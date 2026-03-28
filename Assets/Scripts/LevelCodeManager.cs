using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelCodeManager : MonoBehaviour
{
    [Serializable]
    public enum LevelObjectType : byte
    {
        Unknown = 0,
        Floor = 1,
        Obstacle = 2,
        DirectionTile = 3,
        JumpTile = 4,
        SpeedTile = 5,
        FragileTile = 6,
        Bomb = 7,
        Crate = 8,
        Finish = 9,
        Decor = 10
    }

    [Serializable]
    public class PrefabEntry
    {
        public ushort id;
        public LevelObjectType type;
        public string key;
        public GameObject prefab;
        public bool usesRotation = true;
        public bool includeInBounds = true;
    }

    [Serializable]
    public class ExplosionEffectEntry
    {
        public ushort id;
        public string key;
        public ParticleSystem prefab;
    }

    [Header("Registry")]
    public List<PrefabEntry> prefabs = new List<PrefabEntry>();
    public Transform levelRoot;
    public float tileSize = 1f;
    public ushort baseFloorPrefabId;

    [Header("Bomb Explosion Effects")]
    public List<ExplosionEffectEntry> bombExplosionEffects = new List<ExplosionEffectEntry>();

    [Header("Detection")]
    public string directionTileTag = "Vector";
    public string jumpTileTag = "Jumper";
    public string speedTileTag = "Speed";
    public string fragileTileTag = "FragileTile";
    public string finishTag = "Finish";

    [Header("UI (Optional)")]
    public TMP_InputField codeInput;

    private const string Magic = "TV2";
    private const byte LatestVersion = 7;

    private void OnValidate()
    {
        foreach (var e in prefabs)
        {
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.key) && e.prefab != null)
            {
                e.key = e.prefab.name;
            }
        }

        foreach (var e in bombExplosionEffects)
        {
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.key) && e.prefab != null)
            {
                e.key = e.prefab.name;
            }
        }

        if (baseFloorPrefabId == 0)
        {
            var floors = prefabs.Where(p => p != null && p.type == LevelObjectType.Floor && p.id != 0).ToList();
            if (floors.Count == 1) baseFloorPrefabId = floors[0].id;
        }
    }

    public void ExportToInput()
    {
        string code = ExportLevelCode();
        if (codeInput != null) codeInput.text = code;
        GUIUtility.systemCopyBuffer = code;
    }

    public void ImportFromInput()
    {
        if (codeInput == null) return;
        ImportLevelCode(codeInput.text);
    }

    public string ExportLevelCode()
    {
        var cube = FindAnyObjectByType<DickControlledCube>();
        if (tileSize <= 1e-4f)
        {
            tileSize = cube != null ? cube.tileSize : 1f;
        }

        var ground = CollectGround();
        var instances = CollectInstances();

        if (ground.cells.Count == 0 && instances.Count == 0)
        {
            return EncodeBytes(BuildPayload(new LevelPayload
            {
                originX = 0,
                originZ = 0,
                width = 0,
                height = 0,
                baseFloorPrefabId = baseFloorPrefabId,
                groundYcm = 0,
                floorBitmap = Array.Empty<byte>(),
                floorOverrides = new List<FloorOverrideRecord>(),
                player = cube != null ? CapturePlayer(cube) : default,
                objects = new List<LevelObjectRecord>(),
                bombLinks = new List<BombDetonatorLinkRecord>()
            }));
        }

        ComputeBounds(ground, instances, out int minX, out int minZ, out int maxX, out int maxZ);
        int width = (maxX - minX) + 1;
        int height = (maxZ - minZ) + 1;

        var floorBitmap = BuildFloorBitmap(ground.cells, minX, minZ, width, height);
        var floorOverrides = BuildFloorOverrides(ground.overrides, minX, minZ);

        var records = new List<LevelObjectRecord>(instances.Count);
        foreach (var inst in instances)
        {
            int gx = WorldToGrid(inst.position.x);
            int gz = WorldToGrid(inst.position.z);

            bool hasColor = TryGetExportColor(inst.source, out Color32 color);
            bool hasExplosionEffect = TryGetExportBombExplosionEffect(inst.source, out ushort explosionEffectId);
            records.Add(new LevelObjectRecord
            {
                prefabId = inst.prefabId,
                x = (ushort)(gx - minX),
                z = (ushort)(gz - minZ),
                yCm = (short)Mathf.RoundToInt(inst.position.y * 100f),
                rot = inst.usesRotation ? (byte)RotationToIndex(inst.rotation) : (byte)0,
                hasColor = hasColor,
                color = color,
                hasExplosionEffect = hasExplosionEffect,
                explosionEffectId = explosionEffectId
            });
        }

        records.Sort(CompareRecords);

        var payload = new LevelPayload
        {
            originX = (short)minX,
            originZ = (short)minZ,
            width = (ushort)width,
            height = (ushort)height,
            baseFloorPrefabId = baseFloorPrefabId,
            groundYcm = ground.groundYcm,
            floorBitmap = floorBitmap,
            floorOverrides = floorOverrides,
            player = cube != null ? CapturePlayer(cube) : default,
            objects = records,
            bombLinks = CollectBombLinks(minX, minZ, width)
        };

        return EncodeBytes(BuildPayload(payload));
    }

    public void ImportLevelCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        byte[] bytes;
        try
        {
            bytes = DecodeBytes(code);
        }
        catch (Exception e)
        {
            Debug.LogError($"LevelCode decode failed: {e.Message}");
            return;
        }

        LevelPayload payload;
        try
        {
            payload = ParsePayload(bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"LevelCode parse failed: {e.Message}");
            return;
        }

        ClearGenerated();

        var prefabMap = prefabs
            .Where(p => p != null && p.prefab != null && p.id != 0)
            .GroupBy(p => p.id)
            .ToDictionary(g => g.Key, g => g.First());

        if (payload.width > 0 && payload.height > 0 && payload.floorBitmap != null && payload.floorBitmap.Length > 0 && payload.baseFloorPrefabId != 0)
        {
            if (!prefabMap.TryGetValue(payload.baseFloorPrefabId, out var baseEntry) || baseEntry.prefab == null)
            {
                Debug.LogError($"Base floor prefabId {payload.baseFloorPrefabId} is not registered.");
            }
            else
            {
                var overrideByCell = payload.floorOverrides != null
                    ? payload.floorOverrides.ToDictionary(o => ((int)o.z << 16) | o.x, o => o.prefabId)
                    : new Dictionary<int, ushort>();

                int cellsCount = payload.width * payload.height;
                for (int idx = 0; idx < cellsCount; idx++)
                {
                    if (!GetBit(payload.floorBitmap, idx)) continue;

                    int x = idx % payload.width;
                    int z = idx / payload.width;
                    int key = (z << 16) | x;
                    ushort pid = overrideByCell.TryGetValue(key, out var o) ? o : payload.baseFloorPrefabId;
                    if (!prefabMap.TryGetValue(pid, out var entry) || entry.prefab == null) continue;

                    int gx = payload.originX + x;
                    int gz = payload.originZ + z;
                    float wy = payload.groundYcm / 100f;
                    Vector3 pos = new Vector3(gx * tileSize, wy, gz * tileSize);
                    Instantiate(entry.prefab, pos, Quaternion.identity, levelRoot).name = entry.prefab.name;
                }
            }
        }

        foreach (var rec in payload.objects)
        {
            if (!prefabMap.TryGetValue(rec.prefabId, out var entry) || entry.prefab == null)
                continue;

            int gx = payload.originX + rec.x;
            int gz = payload.originZ + rec.z;
            float wy = rec.yCm / 100f;

            Vector3 pos = new Vector3(gx * tileSize, wy, gz * tileSize);
            Quaternion rot = entry.usesRotation ? IndexToRotation(rec.rot) : Quaternion.identity;

            GameObject obj = Instantiate(entry.prefab, pos, rot, levelRoot);
            obj.name = entry.prefab.name;
            if (rec.hasColor)
            {
                ApplyImportedColor(obj, rec.color);
            }
            if (rec.hasExplosionEffect)
            {
                ApplyImportedBombExplosionEffect(obj, rec.explosionEffectId);
            }
        }

        ApplyBombLinks(payload);

        var cube = FindAnyObjectByType<DickControlledCube>();
        if (cube != null)
        {
            StartCoroutine(ApplyPlayerAfterFrame(cube, payload.player));
        }
    }

    private IEnumerator ApplyPlayerAfterFrame(DickControlledCube cube, PlayerRecord player)
    {
        yield return null;
        ApplyPlayer(cube, player);
    }

    private void ClearGenerated()
    {
        if (levelRoot == null) return;
        for (int i = levelRoot.childCount - 1; i >= 0; i--)
        {
            var child = levelRoot.GetChild(i);
            if (child != null) Destroy(child.gameObject);
        }
    }

    private static int CompareRecords(LevelObjectRecord a, LevelObjectRecord b)
    {
        int c = a.z.CompareTo(b.z);
        if (c != 0) return c;
        c = a.x.CompareTo(b.x);
        if (c != 0) return c;
        c = a.prefabId.CompareTo(b.prefabId);
        if (c != 0) return c;
        c = a.rot.CompareTo(b.rot);
        if (c != 0) return c;
        c = a.yCm.CompareTo(b.yCm);
        if (c != 0) return c;
        c = a.hasColor.CompareTo(b.hasColor);
        if (c != 0) return c;
        if (a.hasColor)
        {
            c = a.color.r.CompareTo(b.color.r);
            if (c != 0) return c;
            c = a.color.g.CompareTo(b.color.g);
            if (c != 0) return c;
            c = a.color.b.CompareTo(b.color.b);
            if (c != 0) return c;
            c = a.color.a.CompareTo(b.color.a);
            if (c != 0) return c;
        }

        c = a.hasExplosionEffect.CompareTo(b.hasExplosionEffect);
        if (c != 0) return c;
        if (a.hasExplosionEffect)
        {
            return a.explosionEffectId.CompareTo(b.explosionEffectId);
        }

        return 0;
    }

    private List<InstanceInfo> CollectInstances()
    {
        var results = new List<InstanceInfo>(256);
        int groundLayer = LayerMask.NameToLayer("Ground");

        IEnumerable<Transform> scope;
        if (levelRoot != null)
        {
            scope = levelRoot.GetComponentsInChildren<Transform>(true).Where(t => t != levelRoot);
        }
        else
        {
            scope = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true));
        }

        foreach (var t in scope)
        {
            if (t == null) continue;
            if (groundLayer != -1 && t.gameObject.layer == groundLayer) continue;
            if (!TryResolveEntry(t.gameObject, out var entry)) continue;

            results.Add(new InstanceInfo
            {
                prefabId = entry.id,
                source = t.gameObject,
                position = t.position,
                rotation = t.rotation,
                usesRotation = entry.usesRotation,
                includeInBounds = entry.includeInBounds
            });
        }

        return results;
    }

    private bool TryResolveEntry(GameObject go, out PrefabEntry entry)
    {
        entry = null;
        if (go == null) return false;

        string nameKey = NormalizeName(go.name);
        entry = prefabs.FirstOrDefault(p => p != null && p.id != 0 && p.prefab != null && NormalizeName(p.key) == nameKey);
        if (entry != null) return true;

        var detectedType = DetectTypeByComponentsAndTags(go);
        if (detectedType == LevelObjectType.Unknown) return false;

        var candidates = prefabs.Where(p => p != null && p.id != 0 && p.prefab != null && p.type == detectedType).ToList();
        if (candidates.Count == 1)
        {
            entry = candidates[0];
            return true;
        }

        // Если объект был переименован в сцене, ключ может не совпасть. Для некоторых типов
        // пытаемся сопоставить по компоненту (Bomb/Detonator/Crate/FragileTile), независимо от type.
        if (go.TryGetComponent<Detonator>(out _))
        {
            var detCandidates = prefabs
                .Where(p => p != null && p.id != 0 && p.prefab != null && p.prefab.GetComponentInChildren<Detonator>(true) != null)
                .ToList();
            if (detCandidates.Count == 1)
            {
                entry = detCandidates[0];
                return true;
            }
        }

        return false;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        string s = name.Replace("(Clone)", string.Empty).Trim();
        return s;
    }

    private LevelObjectType DetectTypeByComponentsAndTags(GameObject go)
    {
        if (go == null) return LevelObjectType.Unknown;

        if (go.TryGetComponent<Bomb>(out _)) return LevelObjectType.Bomb;
        if (go.TryGetComponent<Detonator>(out _)) return LevelObjectType.Decor;
        if (go.TryGetComponent<Crate>(out _)) return LevelObjectType.Crate;
        if (go.TryGetComponent<FragileTile>(out _)) return LevelObjectType.FragileTile;

        if (!string.IsNullOrEmpty(directionTileTag) && go.CompareTag(directionTileTag)) return LevelObjectType.DirectionTile;
        if (!string.IsNullOrEmpty(jumpTileTag) && go.CompareTag(jumpTileTag)) return LevelObjectType.JumpTile;
        if (!string.IsNullOrEmpty(speedTileTag) && go.CompareTag(speedTileTag)) return LevelObjectType.SpeedTile;
        if (!string.IsNullOrEmpty(fragileTileTag) && go.CompareTag(fragileTileTag)) return LevelObjectType.FragileTile;
        if (!string.IsNullOrEmpty(finishTag) && go.CompareTag(finishTag)) return LevelObjectType.Finish;

        return LevelObjectType.Unknown;
    }

    private static bool TryGetExportColor(GameObject go, out Color32 color)
    {
        color = default;
        if (go == null) return false;

        bool wantsColor = go.GetComponent<Bomb>() != null || go.GetComponent<Detonator>() != null;
        if (!wantsColor) return false;

        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return false;

        Color c = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : renderer.material.color;
        color = (Color32)c;
        return true;
    }

    private bool TryGetExportBombExplosionEffect(GameObject go, out ushort explosionEffectId)
    {
        explosionEffectId = 0;
        if (go == null) return false;

        var bomb = go.GetComponent<Bomb>();
        if (bomb == null) return false;

        var fx = bomb.explosionEffect;
        if (fx == null) return false;

        var entry = bombExplosionEffects.FirstOrDefault(e => e != null && e.id != 0 && e.prefab == fx);
        if (entry != null)
        {
            explosionEffectId = entry.id;
            return true;
        }

        string key = NormalizeName(fx.name);
        entry = bombExplosionEffects.FirstOrDefault(e => e != null && e.id != 0 && e.prefab != null && NormalizeName(e.key) == key);
        if (entry != null)
        {
            explosionEffectId = entry.id;
            return true;
        }

        return false;
    }

    private static void ApplyImportedColor(GameObject go, Color32 color)
    {
        if (go == null) return;

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        var bomb = go.GetComponent<Bomb>();
        if (bomb != null)
        {
            bomb.bombColor = color;
        }

        var det = go.GetComponent<Detonator>();
        if (det != null)
        {
            det.detonatorColor = color;
        }
    }

    private void ApplyImportedBombExplosionEffect(GameObject go, ushort explosionEffectId)
    {
        if (go == null) return;
        if (explosionEffectId == 0) return;

        var bomb = go.GetComponent<Bomb>();
        if (bomb == null) return;

        var entry = bombExplosionEffects.FirstOrDefault(e => e != null && e.id == explosionEffectId);
        if (entry == null || entry.prefab == null) return;

        bomb.explosionEffect = entry.prefab;
    }

    private List<BombDetonatorLinkRecord> CollectBombLinks(int minX, int minZ, int width)
    {
        IEnumerable<BombDetonatorPair> pairs;
        if (levelRoot != null)
        {
            pairs = levelRoot.GetComponentsInChildren<BombDetonatorPair>(true);
        }
        else
        {
            pairs = FindObjectsByType<BombDetonatorPair>(FindObjectsSortMode.None);
        }

        var list = new List<BombDetonatorLinkRecord>();
        foreach (var p in pairs)
        {
            if (p == null) continue;
            if (p.links == null || p.links.Count == 0) continue;

            for (int i = 0; i < p.links.Count; i++)
            {
                var link = p.links[i];
                if (link == null || link.bomb == null || link.detonator == null) continue;

                int bgx = WorldToGrid(link.bomb.transform.position.x);
                int bgz = WorldToGrid(link.bomb.transform.position.z);
                int dgx = WorldToGrid(link.detonator.transform.position.x);
                int dgz = WorldToGrid(link.detonator.transform.position.z);

                int brx = bgx - minX;
                int brz = bgz - minZ;
                int drx = dgx - minX;
                int drz = dgz - minZ;

                if (brx < 0 || brz < 0 || drx < 0 || drz < 0) continue;
                if (brx >= width || drx >= width) continue;

                ushort bombCellIndex = (ushort)((brz * width) + brx);
                ushort detCellIndex = (ushort)((drz * width) + drx);

                list.Add(new BombDetonatorLinkRecord
                {
                    detonatorCellIndex = detCellIndex,
                    bombCellIndex = bombCellIndex
                });
            }
        }

        list.Sort((a, b) =>
        {
            int c = a.detonatorCellIndex.CompareTo(b.detonatorCellIndex);
            if (c != 0) return c;
            return a.bombCellIndex.CompareTo(b.bombCellIndex);
        });

        return list;
    }

    private void ApplyBombLinks(LevelPayload payload)
    {
        if (payload.bombLinks == null || payload.bombLinks.Count == 0) return;
        if (levelRoot == null) return;

        var bombs = levelRoot.GetComponentsInChildren<Bomb>(true);
        var dets = levelRoot.GetComponentsInChildren<Detonator>(true);

        var bombsByCell = new Dictionary<ushort, Bomb>(bombs.Length);
        foreach (var bomb in bombs)
        {
            int gx = WorldToGrid(bomb.transform.position.x);
            int gz = WorldToGrid(bomb.transform.position.z);
            int rx = gx - payload.originX;
            int rz = gz - payload.originZ;
            if (rx < 0 || rz < 0 || rx >= payload.width || rz >= payload.height) continue;
            ushort cellIndex = (ushort)((rz * payload.width) + rx);
            if (!bombsByCell.ContainsKey(cellIndex)) bombsByCell[cellIndex] = bomb;
        }

        var detsByCell = new Dictionary<ushort, Detonator>(dets.Length);
        foreach (var det in dets)
        {
            int gx = WorldToGrid(det.transform.position.x);
            int gz = WorldToGrid(det.transform.position.z);
            int rx = gx - payload.originX;
            int rz = gz - payload.originZ;
            if (rx < 0 || rz < 0 || rx >= payload.width || rz >= payload.height) continue;
            ushort cellIndex = (ushort)((rz * payload.width) + rx);
            if (!detsByCell.ContainsKey(cellIndex)) detsByCell[cellIndex] = det;
        }

        foreach (var link in payload.bombLinks)
        {
            if (!detsByCell.TryGetValue(link.detonatorCellIndex, out var det)) continue;
            if (!bombsByCell.TryGetValue(link.bombCellIndex, out var bomb)) continue;

            det.onPressed.AddListener(bomb.Activate);

            bomb.bombColor = det.detonatorColor;
            var rend = bomb.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = det.detonatorColor;
            }
        }
    }

    private void ComputeBounds(GroundInfo ground, List<InstanceInfo> instances, out int minX, out int minZ, out int maxX, out int maxZ)
    {
        bool first = true;
        minX = minZ = maxX = maxZ = 0;

        foreach (var cell in ground.cells)
        {
            int gx = cell.x;
            int gz = cell.z;
            if (first)
            {
                minX = maxX = gx;
                minZ = maxZ = gz;
                first = false;
            }
            else
            {
                if (gx < minX) minX = gx;
                if (gx > maxX) maxX = gx;
                if (gz < minZ) minZ = gz;
                if (gz > maxZ) maxZ = gz;
            }
        }

        foreach (var inst in instances)
        {
            if (!inst.includeInBounds) continue;
            int gx = WorldToGrid(inst.position.x);
            int gz = WorldToGrid(inst.position.z);
            if (first)
            {
                minX = maxX = gx;
                minZ = maxZ = gz;
                first = false;
            }
            else
            {
                if (gx < minX) minX = gx;
                if (gx > maxX) maxX = gx;
                if (gz < minZ) minZ = gz;
                if (gz > maxZ) maxZ = gz;
            }
        }

        if (first)
        {
            minX = minZ = maxX = maxZ = 0;
        }
    }

    private int WorldToGrid(float world)
    {
        return Mathf.RoundToInt(world / tileSize);
    }

    private static int RotationToIndex(Quaternion rot)
    {
        float y = rot.eulerAngles.y;
        int idx = Mathf.RoundToInt(y / 90f) % 4;
        if (idx < 0) idx += 4;
        return idx;
    }

    private static Quaternion IndexToRotation(byte idx)
    {
        int i = idx % 4;
        return Quaternion.Euler(0f, i * 90f, 0f);
    }

    private static byte[] BuildPayload(LevelPayload payload)
    {
        if (CanUseV7(payload)) return BuildPayloadV7(payload);
        if (CanUseV6(payload)) return BuildPayloadV6(payload);
        if (CanUseV5(payload)) return BuildPayloadV5(payload);
        if (CanUseV4(payload)) return BuildPayloadV4(payload);
        return BuildPayloadV3(payload);
    }

    private static bool CanUseV7(LevelPayload payload)
    {
        if (!CanUseV6(payload)) return false;
        if (payload.bombLinks == null) return false;
        if (payload.bombLinks.Count > ushort.MaxValue) return false;
        return true;
    }

    private static bool CanUseV6(LevelPayload payload)
    {
        return CanUseV5(payload);
    }

    private static bool CanUseV5(LevelPayload payload)
    {
        if (!CanUseV4(payload)) return false;
        return true;
    }

    private static bool CanUseV4(LevelPayload payload)
    {
        if (payload.width == 0 || payload.height == 0) return false;
        int cellCount = payload.width * payload.height;
        if (cellCount <= 0 || cellCount > ushort.MaxValue) return false;

        if (payload.floorBitmap == null) return false;
        if (payload.floorBitmap.Length > ushort.MaxValue) return false;

        if (payload.floorOverrides == null) return false;
        if (payload.floorOverrides.Count > ushort.MaxValue) return false;

        if (payload.objects == null) return false;
        if (payload.objects.Count > ushort.MaxValue) return false;

        var palette = BuildObjectPalette(payload.objects);
        if (palette.Count > byte.MaxValue) return false;

        return true;
    }

    private static byte[] BuildPayloadV3(LevelPayload payload)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)Magic[0]);
        bw.Write((byte)Magic[1]);
        bw.Write((byte)Magic[2]);
        bw.Write((byte)3);

        bw.Write(payload.originX);
        bw.Write(payload.originZ);
        bw.Write(payload.width);
        bw.Write(payload.height);
        bw.Write(payload.baseFloorPrefabId);
        bw.Write(payload.groundYcm);

        bw.Write((int)payload.floorBitmap.Length);
        bw.Write(payload.floorBitmap);

        bw.Write((ushort)payload.floorOverrides.Count);
        foreach (var o in payload.floorOverrides)
        {
            bw.Write(o.prefabId);
            bw.Write(o.x);
            bw.Write(o.z);
        }

        bw.Write(payload.player.x);
        bw.Write(payload.player.z);
        bw.Write(payload.player.yCm);
        bw.Write(payload.player.rot);

        bw.Write((ushort)payload.objects.Count);
        foreach (var o in payload.objects)
        {
            bw.Write(o.prefabId);
            bw.Write(o.x);
            bw.Write(o.z);
            bw.Write(o.yCm);
            bw.Write(o.rot);
        }

        bw.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildPayloadV4(LevelPayload payload)
    {
        int width = payload.width;
        int height = payload.height;
        int cellCount = width * height;

        var palette = BuildObjectPalette(payload.objects);
        var paletteIndexByPrefabId = palette
            .Select((pid, i) => new { pid, i })
            .ToDictionary(x => x.pid, x => (byte)x.i);

        ushort commonOverridePrefabId = 0;
        if (payload.floorOverrides.Count > 0)
        {
            commonOverridePrefabId = payload.floorOverrides[0].prefabId;
            for (int i = 1; i < payload.floorOverrides.Count; i++)
            {
                if (payload.floorOverrides[i].prefabId != commonOverridePrefabId)
                {
                    commonOverridePrefabId = 0;
                    break;
                }
            }
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)Magic[0]);
        bw.Write((byte)Magic[1]);
        bw.Write((byte)Magic[2]);
        bw.Write((byte)4);

        bw.Write(payload.originX);
        bw.Write(payload.originZ);
        bw.Write(payload.width);
        bw.Write(payload.height);

        bw.Write(payload.baseFloorPrefabId);
        bw.Write(payload.groundYcm);

        bw.Write((ushort)payload.floorBitmap.Length);
        bw.Write(payload.floorBitmap);

        bw.Write(commonOverridePrefabId);
        bw.Write((ushort)payload.floorOverrides.Count);
        for (int i = 0; i < payload.floorOverrides.Count; i++)
        {
            var o = payload.floorOverrides[i];
            ushort cellIndex = (ushort)((o.z * width) + o.x);
            if (commonOverridePrefabId == 0)
            {
                bw.Write(o.prefabId);
            }
            bw.Write(cellIndex);
        }

        bw.Write(payload.player.x);
        bw.Write(payload.player.z);
        bw.Write(payload.player.yCm);
        bw.Write(payload.player.rot);

        bw.Write((byte)palette.Count);
        for (int i = 0; i < palette.Count; i++)
        {
            bw.Write(palette[i]);
        }

        bw.Write((ushort)payload.objects.Count);
        for (int i = 0; i < payload.objects.Count; i++)
        {
            var o = payload.objects[i];
            ushort cellIndex = (ushort)((o.z * width) + o.x);
            byte palIdx = paletteIndexByPrefabId[o.prefabId];

            short yDelta = (short)(o.yCm - payload.groundYcm);
            bool hasY = yDelta != 0;
            byte meta = (byte)((o.rot & 3) | (hasY ? 4 : 0));

            bw.Write(cellIndex);
            bw.Write(palIdx);
            bw.Write(meta);
            if (hasY)
            {
                bw.Write(yDelta);
            }
        }

        bw.Flush();
        return ms.ToArray();
    }

    private static List<ushort> BuildObjectPalette(List<LevelObjectRecord> objects)
    {
        var set = new HashSet<ushort>();
        for (int i = 0; i < objects.Count; i++)
        {
            set.Add(objects[i].prefabId);
        }

        var list = set.ToList();
        list.Sort();
        return list;
    }

    private static byte[] BuildPayloadV5(LevelPayload payload)
    {
        int width = payload.width;
        int height = payload.height;
        int cellCount = width * height;

        var palette = BuildObjectPalette(payload.objects);
        var paletteIndexByPrefabId = palette
            .Select((pid, i) => new { pid, i })
            .ToDictionary(x => x.pid, x => (byte)x.i);

        ushort commonOverridePrefabId = 0;
        if (payload.floorOverrides.Count > 0)
        {
            commonOverridePrefabId = payload.floorOverrides[0].prefabId;
            for (int i = 1; i < payload.floorOverrides.Count; i++)
            {
                if (payload.floorOverrides[i].prefabId != commonOverridePrefabId)
                {
                    commonOverridePrefabId = 0;
                    break;
                }
            }
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)Magic[0]);
        bw.Write((byte)Magic[1]);
        bw.Write((byte)Magic[2]);
        bw.Write((byte)5);

        bw.Write(payload.originX);
        bw.Write(payload.originZ);
        bw.Write(payload.width);
        bw.Write(payload.height);

        bw.Write(payload.baseFloorPrefabId);
        bw.Write(payload.groundYcm);

        bw.Write((ushort)payload.floorBitmap.Length);
        bw.Write(payload.floorBitmap);

        bw.Write(commonOverridePrefabId);
        bw.Write((ushort)payload.floorOverrides.Count);
        for (int i = 0; i < payload.floorOverrides.Count; i++)
        {
            var o = payload.floorOverrides[i];
            ushort cellIndex = (ushort)((o.z * width) + o.x);
            if (commonOverridePrefabId == 0)
            {
                bw.Write(o.prefabId);
            }
            bw.Write(cellIndex);
        }

        bw.Write(payload.player.x);
        bw.Write(payload.player.z);
        bw.Write(payload.player.yCm);
        bw.Write(payload.player.rot);

        bw.Write((byte)palette.Count);
        for (int i = 0; i < palette.Count; i++)
        {
            bw.Write(palette[i]);
        }

        bw.Write((ushort)payload.objects.Count);
        for (int i = 0; i < payload.objects.Count; i++)
        {
            var o = payload.objects[i];
            ushort cellIndex = (ushort)((o.z * width) + o.x);
            byte palIdx = paletteIndexByPrefabId[o.prefabId];

            short yDelta = (short)(o.yCm - payload.groundYcm);
            bool hasY = yDelta != 0;
            bool hasColor = o.hasColor;
            byte meta = (byte)((o.rot & 3) | (hasY ? 4 : 0) | (hasColor ? 8 : 0));

            bw.Write(cellIndex);
            bw.Write(palIdx);
            bw.Write(meta);
            if (hasY)
            {
                bw.Write(yDelta);
            }
            if (hasColor)
            {
                bw.Write(o.color.r);
                bw.Write(o.color.g);
                bw.Write(o.color.b);
                bw.Write(o.color.a);
            }
        }

        bw.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildPayloadV6(LevelPayload payload)
    {
        int width = payload.width;
        int height = payload.height;
        int cellCount = width * height;

        var palette = BuildObjectPalette(payload.objects);
        var paletteIndexByPrefabId = palette
            .Select((pid, i) => new { pid, i })
            .ToDictionary(x => x.pid, x => (byte)x.i);

        ushort commonOverridePrefabId = 0;
        if (payload.floorOverrides.Count > 0)
        {
            commonOverridePrefabId = payload.floorOverrides[0].prefabId;
            for (int i = 1; i < payload.floorOverrides.Count; i++)
            {
                if (payload.floorOverrides[i].prefabId != commonOverridePrefabId)
                {
                    commonOverridePrefabId = 0;
                    break;
                }
            }
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)Magic[0]);
        bw.Write((byte)Magic[1]);
        bw.Write((byte)Magic[2]);
        bw.Write((byte)6);

        bw.Write(payload.originX);
        bw.Write(payload.originZ);
        bw.Write(payload.width);
        bw.Write(payload.height);

        bw.Write(payload.baseFloorPrefabId);
        bw.Write(payload.groundYcm);

        bw.Write((ushort)payload.floorBitmap.Length);
        bw.Write(payload.floorBitmap);

        bw.Write(commonOverridePrefabId);
        bw.Write((ushort)payload.floorOverrides.Count);
        for (int i = 0; i < payload.floorOverrides.Count; i++)
        {
            var o = payload.floorOverrides[i];
            ushort cellIndex = (ushort)((o.z * width) + o.x);
            if (commonOverridePrefabId == 0)
            {
                bw.Write(o.prefabId);
            }
            bw.Write(cellIndex);
        }

        bw.Write(payload.player.x);
        bw.Write(payload.player.z);
        bw.Write(payload.player.yCm);
        bw.Write(payload.player.rot);

        bw.Write((byte)palette.Count);
        for (int i = 0; i < palette.Count; i++)
        {
            bw.Write(palette[i]);
        }

        bw.Write((ushort)payload.objects.Count);
        for (int i = 0; i < payload.objects.Count; i++)
        {
            var o = payload.objects[i];
            ushort cellIndex = (ushort)((o.z * width) + o.x);
            byte palIdx = paletteIndexByPrefabId[o.prefabId];

            short yDelta = (short)(o.yCm - payload.groundYcm);
            bool hasY = yDelta != 0;
            bool hasColor = o.hasColor;
            bool hasExplosionEffect = o.hasExplosionEffect;
            byte meta = (byte)((o.rot & 3) | (hasY ? 4 : 0) | (hasColor ? 8 : 0) | (hasExplosionEffect ? 16 : 0));

            bw.Write(cellIndex);
            bw.Write(palIdx);
            bw.Write(meta);
            if (hasY)
            {
                bw.Write(yDelta);
            }
            if (hasColor)
            {
                bw.Write(o.color.r);
                bw.Write(o.color.g);
                bw.Write(o.color.b);
                bw.Write(o.color.a);
            }
            if (hasExplosionEffect)
            {
                bw.Write(o.explosionEffectId);
            }
        }

        bw.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildPayloadV7(LevelPayload payload)
    {
        int width = payload.width;
        int height = payload.height;
        int cellCount = width * height;

        var palette = BuildObjectPalette(payload.objects);
        var paletteIndexByPrefabId = palette
            .Select((pid, i) => new { pid, i })
            .ToDictionary(x => x.pid, x => (byte)x.i);

        ushort commonOverridePrefabId = 0;
        if (payload.floorOverrides.Count > 0)
        {
            commonOverridePrefabId = payload.floorOverrides[0].prefabId;
            for (int i = 1; i < payload.floorOverrides.Count; i++)
            {
                if (payload.floorOverrides[i].prefabId != commonOverridePrefabId)
                {
                    commonOverridePrefabId = 0;
                    break;
                }
            }
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)Magic[0]);
        bw.Write((byte)Magic[1]);
        bw.Write((byte)Magic[2]);
        bw.Write(LatestVersion);

        bw.Write(payload.originX);
        bw.Write(payload.originZ);
        bw.Write(payload.width);
        bw.Write(payload.height);

        bw.Write(payload.baseFloorPrefabId);
        bw.Write(payload.groundYcm);

        bw.Write((ushort)payload.floorBitmap.Length);
        bw.Write(payload.floorBitmap);

        bw.Write(commonOverridePrefabId);
        bw.Write((ushort)payload.floorOverrides.Count);
        for (int i = 0; i < payload.floorOverrides.Count; i++)
        {
            var o = payload.floorOverrides[i];
            ushort cellIndex = (ushort)((o.z * width) + o.x);
            if (commonOverridePrefabId == 0)
            {
                bw.Write(o.prefabId);
            }
            bw.Write(cellIndex);
        }

        bw.Write(payload.player.x);
        bw.Write(payload.player.z);
        bw.Write(payload.player.yCm);
        bw.Write(payload.player.rot);

        bw.Write((byte)palette.Count);
        for (int i = 0; i < palette.Count; i++)
        {
            bw.Write(palette[i]);
        }

        bw.Write((ushort)payload.objects.Count);
        for (int i = 0; i < payload.objects.Count; i++)
        {
            var o = payload.objects[i];
            ushort cellIndex = (ushort)((o.z * width) + o.x);
            byte palIdx = paletteIndexByPrefabId[o.prefabId];

            short yDelta = (short)(o.yCm - payload.groundYcm);
            bool hasY = yDelta != 0;
            bool hasColor = o.hasColor;
            bool hasExplosionEffect = o.hasExplosionEffect;
            byte meta = (byte)((o.rot & 3) | (hasY ? 4 : 0) | (hasColor ? 8 : 0) | (hasExplosionEffect ? 16 : 0));

            bw.Write(cellIndex);
            bw.Write(palIdx);
            bw.Write(meta);
            if (hasY)
            {
                bw.Write(yDelta);
            }
            if (hasColor)
            {
                bw.Write(o.color.r);
                bw.Write(o.color.g);
                bw.Write(o.color.b);
                bw.Write(o.color.a);
            }
            if (hasExplosionEffect)
            {
                bw.Write(o.explosionEffectId);
            }
        }

        bw.Write((ushort)payload.bombLinks.Count);
        for (int i = 0; i < payload.bombLinks.Count; i++)
        {
            bw.Write(payload.bombLinks[i].detonatorCellIndex);
            bw.Write(payload.bombLinks[i].bombCellIndex);
        }

        bw.Flush();
        return ms.ToArray();
    }

    private static LevelPayload ParsePayload(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);

        byte m0 = br.ReadByte();
        byte m1 = br.ReadByte();
        byte m2 = br.ReadByte();
        byte ver = br.ReadByte();

        if (m0 != (byte)Magic[0] || m1 != (byte)Magic[1] || m2 != (byte)Magic[2])
            throw new InvalidDataException("Bad magic");
        if (ver != 2 && ver != 3 && ver != 4 && ver != 5 && ver != 6 && ver != 7)
            throw new InvalidDataException($"Unsupported version {ver}");

        var payload = new LevelPayload();
        payload.originX = br.ReadInt16();
        payload.originZ = br.ReadInt16();
        payload.width = br.ReadUInt16();
        payload.height = br.ReadUInt16();

        if (ver == 7)
        {
            payload.baseFloorPrefabId = br.ReadUInt16();
            payload.groundYcm = br.ReadInt16();

            ushort bitmapLen = br.ReadUInt16();
            payload.floorBitmap = bitmapLen > 0 ? br.ReadBytes(bitmapLen) : Array.Empty<byte>();

            ushort commonOverridePrefabId = br.ReadUInt16();
            ushort overridesCount = br.ReadUInt16();
            payload.floorOverrides = new List<FloorOverrideRecord>(overridesCount);
            for (int i = 0; i < overridesCount; i++)
            {
                ushort prefabId = commonOverridePrefabId != 0 ? commonOverridePrefabId : br.ReadUInt16();
                ushort cellIndex = br.ReadUInt16();
                ushort x = (ushort)(cellIndex % payload.width);
                ushort z = (ushort)(cellIndex / payload.width);
                payload.floorOverrides.Add(new FloorOverrideRecord
                {
                    prefabId = prefabId,
                    x = x,
                    z = z
                });
            }

            payload.player = new PlayerRecord
            {
                x = br.ReadInt16(),
                z = br.ReadInt16(),
                yCm = br.ReadInt16(),
                rot = br.ReadByte()
            };

            byte paletteCount = br.ReadByte();
            var palette = new ushort[paletteCount];
            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = br.ReadUInt16();
            }

            ushort objCount = br.ReadUInt16();
            payload.objects = new List<LevelObjectRecord>(objCount);
            for (int i = 0; i < objCount; i++)
            {
                ushort cellIndex = br.ReadUInt16();
                byte palIdx = br.ReadByte();
                byte meta = br.ReadByte();
                byte rot = (byte)(meta & 3);
                bool hasY = (meta & 4) != 0;
                bool hasColor = (meta & 8) != 0;
                bool hasExplosionEffect = (meta & 16) != 0;
                short yDelta = hasY ? br.ReadInt16() : (short)0;
                Color32 color = default;
                if (hasColor)
                {
                    byte r = br.ReadByte();
                    byte g = br.ReadByte();
                    byte b = br.ReadByte();
                    byte a = br.ReadByte();
                    color = new Color32(r, g, b, a);
                }

                ushort explosionEffectId = hasExplosionEffect ? br.ReadUInt16() : (ushort)0;

                ushort x = (ushort)(cellIndex % payload.width);
                ushort z = (ushort)(cellIndex / payload.width);

                payload.objects.Add(new LevelObjectRecord
                {
                    prefabId = palette[palIdx],
                    x = x,
                    z = z,
                    yCm = (short)(payload.groundYcm + yDelta),
                    rot = rot,
                    hasColor = hasColor,
                    color = color,
                    hasExplosionEffect = hasExplosionEffect,
                    explosionEffectId = explosionEffectId
                });
            }

            ushort linkCount = br.ReadUInt16();
            payload.bombLinks = new List<BombDetonatorLinkRecord>(linkCount);
            for (int i = 0; i < linkCount; i++)
            {
                payload.bombLinks.Add(new BombDetonatorLinkRecord
                {
                    detonatorCellIndex = br.ReadUInt16(),
                    bombCellIndex = br.ReadUInt16()
                });
            }

            return payload;
        }

        if (ver == 6)
        {
            payload.baseFloorPrefabId = br.ReadUInt16();
            payload.groundYcm = br.ReadInt16();

            ushort bitmapLen = br.ReadUInt16();
            payload.floorBitmap = bitmapLen > 0 ? br.ReadBytes(bitmapLen) : Array.Empty<byte>();

            ushort commonOverridePrefabId = br.ReadUInt16();
            ushort overridesCount = br.ReadUInt16();
            payload.floorOverrides = new List<FloorOverrideRecord>(overridesCount);
            for (int i = 0; i < overridesCount; i++)
            {
                ushort prefabId = commonOverridePrefabId != 0 ? commonOverridePrefabId : br.ReadUInt16();
                ushort cellIndex = br.ReadUInt16();
                ushort x = (ushort)(cellIndex % payload.width);
                ushort z = (ushort)(cellIndex / payload.width);
                payload.floorOverrides.Add(new FloorOverrideRecord
                {
                    prefabId = prefabId,
                    x = x,
                    z = z
                });
            }

            payload.player = new PlayerRecord
            {
                x = br.ReadInt16(),
                z = br.ReadInt16(),
                yCm = br.ReadInt16(),
                rot = br.ReadByte()
            };

            byte paletteCount = br.ReadByte();
            var palette = new ushort[paletteCount];
            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = br.ReadUInt16();
            }

            ushort objCount = br.ReadUInt16();
            payload.objects = new List<LevelObjectRecord>(objCount);
            for (int i = 0; i < objCount; i++)
            {
                ushort cellIndex = br.ReadUInt16();
                byte palIdx = br.ReadByte();
                byte meta = br.ReadByte();
                byte rot = (byte)(meta & 3);
                bool hasY = (meta & 4) != 0;
                bool hasColor = (meta & 8) != 0;
                bool hasExplosionEffect = (meta & 16) != 0;
                short yDelta = hasY ? br.ReadInt16() : (short)0;
                Color32 color = default;
                if (hasColor)
                {
                    byte r = br.ReadByte();
                    byte g = br.ReadByte();
                    byte b = br.ReadByte();
                    byte a = br.ReadByte();
                    color = new Color32(r, g, b, a);
                }

                ushort explosionEffectId = hasExplosionEffect ? br.ReadUInt16() : (ushort)0;

                ushort x = (ushort)(cellIndex % payload.width);
                ushort z = (ushort)(cellIndex / payload.width);

                payload.objects.Add(new LevelObjectRecord
                {
                    prefabId = palette[palIdx],
                    x = x,
                    z = z,
                    yCm = (short)(payload.groundYcm + yDelta),
                    rot = rot,
                    hasColor = hasColor,
                    color = color,
                    hasExplosionEffect = hasExplosionEffect,
                    explosionEffectId = explosionEffectId
                });
            }

            return payload;
        }

        if (ver == 5)
        {
            payload.baseFloorPrefabId = br.ReadUInt16();
            payload.groundYcm = br.ReadInt16();

            ushort bitmapLen = br.ReadUInt16();
            payload.floorBitmap = bitmapLen > 0 ? br.ReadBytes(bitmapLen) : Array.Empty<byte>();

            ushort commonOverridePrefabId = br.ReadUInt16();
            ushort overridesCount = br.ReadUInt16();
            payload.floorOverrides = new List<FloorOverrideRecord>(overridesCount);
            for (int i = 0; i < overridesCount; i++)
            {
                ushort prefabId = commonOverridePrefabId != 0 ? commonOverridePrefabId : br.ReadUInt16();
                ushort cellIndex = br.ReadUInt16();
                ushort x = (ushort)(cellIndex % payload.width);
                ushort z = (ushort)(cellIndex / payload.width);
                payload.floorOverrides.Add(new FloorOverrideRecord
                {
                    prefabId = prefabId,
                    x = x,
                    z = z
                });
            }

            payload.player = new PlayerRecord
            {
                x = br.ReadInt16(),
                z = br.ReadInt16(),
                yCm = br.ReadInt16(),
                rot = br.ReadByte()
            };

            byte paletteCount = br.ReadByte();
            var palette = new ushort[paletteCount];
            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = br.ReadUInt16();
            }

            ushort objCount = br.ReadUInt16();
            payload.objects = new List<LevelObjectRecord>(objCount);
            for (int i = 0; i < objCount; i++)
            {
                ushort cellIndex = br.ReadUInt16();
                byte palIdx = br.ReadByte();
                byte meta = br.ReadByte();
                byte rot = (byte)(meta & 3);
                bool hasY = (meta & 4) != 0;
                bool hasColor = (meta & 8) != 0;
                short yDelta = hasY ? br.ReadInt16() : (short)0;
                Color32 color = default;
                if (hasColor)
                {
                    byte r = br.ReadByte();
                    byte g = br.ReadByte();
                    byte b = br.ReadByte();
                    byte a = br.ReadByte();
                    color = new Color32(r, g, b, a);
                }

                ushort x = (ushort)(cellIndex % payload.width);
                ushort z = (ushort)(cellIndex / payload.width);

                payload.objects.Add(new LevelObjectRecord
                {
                    prefabId = palette[palIdx],
                    x = x,
                    z = z,
                    yCm = (short)(payload.groundYcm + yDelta),
                    rot = rot,
                    hasColor = hasColor,
                    color = color,
                    hasExplosionEffect = false,
                    explosionEffectId = 0
                });
            }

            return payload;
        }

        if (ver == 4)
        {
            payload.baseFloorPrefabId = br.ReadUInt16();
            payload.groundYcm = br.ReadInt16();

            ushort bitmapLen = br.ReadUInt16();
            payload.floorBitmap = bitmapLen > 0 ? br.ReadBytes(bitmapLen) : Array.Empty<byte>();

            ushort commonOverridePrefabId = br.ReadUInt16();
            ushort overridesCount = br.ReadUInt16();
            payload.floorOverrides = new List<FloorOverrideRecord>(overridesCount);
            for (int i = 0; i < overridesCount; i++)
            {
                ushort prefabId = commonOverridePrefabId != 0 ? commonOverridePrefabId : br.ReadUInt16();
                ushort cellIndex = br.ReadUInt16();
                ushort x = (ushort)(cellIndex % payload.width);
                ushort z = (ushort)(cellIndex / payload.width);
                payload.floorOverrides.Add(new FloorOverrideRecord
                {
                    prefabId = prefabId,
                    x = x,
                    z = z
                });
            }

            payload.player = new PlayerRecord
            {
                x = br.ReadInt16(),
                z = br.ReadInt16(),
                yCm = br.ReadInt16(),
                rot = br.ReadByte()
            };

            byte paletteCount = br.ReadByte();
            var palette = new ushort[paletteCount];
            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = br.ReadUInt16();
            }

            ushort objCount = br.ReadUInt16();
            payload.objects = new List<LevelObjectRecord>(objCount);
            for (int i = 0; i < objCount; i++)
            {
                ushort cellIndex = br.ReadUInt16();
                byte palIdx = br.ReadByte();
                byte meta = br.ReadByte();
                byte rot = (byte)(meta & 3);
                bool hasY = (meta & 4) != 0;
                short yDelta = hasY ? br.ReadInt16() : (short)0;

                ushort x = (ushort)(cellIndex % payload.width);
                ushort z = (ushort)(cellIndex / payload.width);

                payload.objects.Add(new LevelObjectRecord
                {
                    prefabId = palette[palIdx],
                    x = x,
                    z = z,
                    yCm = (short)(payload.groundYcm + yDelta),
                    rot = rot,
                    hasColor = false,
                    color = default,
                    hasExplosionEffect = false,
                    explosionEffectId = 0
                });
            }

            return payload;
        }

        if (ver == 3)
        {
            payload.baseFloorPrefabId = br.ReadUInt16();
            payload.groundYcm = br.ReadInt16();

            int bitmapLen = br.ReadInt32();
            payload.floorBitmap = bitmapLen > 0 ? br.ReadBytes(bitmapLen) : Array.Empty<byte>();

            ushort overridesCount = br.ReadUInt16();
            payload.floorOverrides = new List<FloorOverrideRecord>(overridesCount);
            for (int i = 0; i < overridesCount; i++)
            {
                payload.floorOverrides.Add(new FloorOverrideRecord
                {
                    prefabId = br.ReadUInt16(),
                    x = br.ReadUInt16(),
                    z = br.ReadUInt16()
                });
            }
        }
        else
        {
            payload.baseFloorPrefabId = 0;
            payload.groundYcm = 0;
            payload.floorBitmap = Array.Empty<byte>();
            payload.floorOverrides = new List<FloorOverrideRecord>();
        }

        payload.player = new PlayerRecord
        {
            x = br.ReadInt16(),
            z = br.ReadInt16(),
            yCm = br.ReadInt16(),
            rot = br.ReadByte()
        };
        payload.objects = new List<LevelObjectRecord>();
        payload.bombLinks = new List<BombDetonatorLinkRecord>();

        ushort count = br.ReadUInt16();
        payload.objects.Capacity = count;
        for (int i = 0; i < count; i++)
        {
            payload.objects.Add(new LevelObjectRecord
            {
                prefabId = br.ReadUInt16(),
                x = br.ReadUInt16(),
                z = br.ReadUInt16(),
                yCm = br.ReadInt16(),
                rot = br.ReadByte(),
                hasColor = false,
                color = default,
                hasExplosionEffect = false,
                explosionEffectId = 0
            });
        }

        return payload;
    }

    private GroundInfo CollectGround()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer == -1)
        {
            return new GroundInfo
            {
                cells = new HashSet<GridCell>(),
                overrides = new Dictionary<GridCell, ushort>(),
                groundYcm = 0
            };
        }

        IEnumerable<Transform> scope;
        if (levelRoot != null)
        {
            scope = levelRoot.GetComponentsInChildren<Transform>(true).Where(t => t != levelRoot);
        }
        else
        {
            scope = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true));
        }

        var cells = new HashSet<GridCell>();
        var overrides = new Dictionary<GridCell, ushort>();
        int groundYcm = 0;
        var yFreq = new Dictionary<int, int>(16);

        foreach (var t in scope)
        {
            if (t == null) continue;
            var go = t.gameObject;
            if (go.layer != groundLayer) continue;

            int gx = WorldToGrid(t.position.x);
            int gz = WorldToGrid(t.position.z);
            var cell = new GridCell((short)gx, (short)gz);
            cells.Add(cell);

            int ycm = Mathf.RoundToInt(t.position.y * 100f);
            if (yFreq.TryGetValue(ycm, out var cnt)) yFreq[ycm] = cnt + 1; else yFreq[ycm] = 1;

            if ((!string.IsNullOrEmpty(fragileTileTag) && go.CompareTag(fragileTileTag)) || go.GetComponent<FragileTile>() != null)
            {
                if (TryResolveEntry(go, out var entry) && entry.id != 0)
                {
                    overrides[cell] = entry.id;
                }
            }
        }

        if (yFreq.Count > 0)
        {
            int bestY = 0, bestCount = -1;
            foreach (var kv in yFreq)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    bestY = kv.Key;
                }
            }
            groundYcm = bestY;
        }

        return new GroundInfo
        {
            cells = cells,
            overrides = overrides,
            groundYcm = (short)groundYcm
        };
    }

    private byte[] BuildFloorBitmap(HashSet<GridCell> cells, int minX, int minZ, int width, int height)
    {
        int total = width * height;
        var bitmap = new byte[(total + 7) / 8];
        foreach (var cell in cells)
        {
            int x = cell.x - minX;
            int z = cell.z - minZ;
            if (x < 0 || z < 0 || x >= width || z >= height) continue;
            int idx = z * width + x;
            SetBit(bitmap, idx);
        }
        return bitmap;
    }

    private List<FloorOverrideRecord> BuildFloorOverrides(Dictionary<GridCell, ushort> overrides, int minX, int minZ)
    {
        var list = new List<FloorOverrideRecord>(overrides.Count);
        foreach (var kv in overrides)
        {
            int x = kv.Key.x - minX;
            int z = kv.Key.z - minZ;
            if (x < 0 || z < 0) continue;
            list.Add(new FloorOverrideRecord
            {
                prefabId = kv.Value,
                x = (ushort)x,
                z = (ushort)z
            });
        }
        list.Sort((a, b) =>
        {
            int c = a.z.CompareTo(b.z);
            if (c != 0) return c;
            c = a.x.CompareTo(b.x);
            if (c != 0) return c;
            return a.prefabId.CompareTo(b.prefabId);
        });
        return list;
    }

    private static bool GetBit(byte[] bitmap, int idx)
    {
        int byteIdx = idx >> 3;
        int bit = idx & 7;
        if (byteIdx < 0 || byteIdx >= bitmap.Length) return false;
        return (bitmap[byteIdx] & (1 << bit)) != 0;
    }

    private static void SetBit(byte[] bitmap, int idx)
    {
        int byteIdx = idx >> 3;
        int bit = idx & 7;
        bitmap[byteIdx] |= (byte)(1 << bit);
    }

    private static string EncodeBytes(byte[] payload)
    {
        byte[] compressed = Compress(payload);
        string b64 = Convert.ToBase64String(compressed);
        b64 = b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return b64;
    }

    private static byte[] DecodeBytes(string code)
    {
        string b64 = code.Trim().Replace('-', '+').Replace('_', '/');
        switch (b64.Length % 4)
        {
            case 2: b64 += "=="; break;
            case 3: b64 += "="; break;
        }

        byte[] compressed = Convert.FromBase64String(b64);
        return Decompress(compressed);
    }

    private static byte[] Compress(byte[] input)
    {
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            ds.Write(input, 0, input.Length);
        }
        return ms.ToArray();
    }

    private static byte[] Decompress(byte[] input)
    {
        using var src = new MemoryStream(input);
        using var ds = new DeflateStream(src, CompressionMode.Decompress);
        using var dst = new MemoryStream();
        ds.CopyTo(dst);
        return dst.ToArray();
    }

    private static PlayerRecord CapturePlayer(DickControlledCube cube)
    {
        Vector3 p = cube.transform.position;
        int gx = Mathf.RoundToInt(p.x / cube.tileSize);
        int gz = Mathf.RoundToInt(p.z / cube.tileSize);

        Vector3 dir = cube.mainPointer != null ? cube.mainPointer.forward : cube.transform.forward;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;

        return new PlayerRecord
        {
            x = (short)gx,
            z = (short)gz,
            yCm = (short)Mathf.RoundToInt(p.y * 100f),
            rot = (byte)RotationToIndex(Quaternion.LookRotation(dir))
        };
    }

    private void ApplyPlayer(DickControlledCube cube, PlayerRecord player)
    {
        float wy = player.yCm / 100f;
        Vector3 pos = new Vector3(player.x * tileSize, wy, player.z * tileSize);
        Vector3 dir = IndexToRotation(player.rot) * Vector3.forward;

        if (cube.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        cube.movementEnabled = false;
        cube.transform.position = pos;
        cube.transform.rotation = Quaternion.LookRotation(dir);

        cube.InitialPosition = pos;
        cube.InitialDirection = dir;

        cube.ForceUpdateDirection(dir);
        Physics.SyncTransforms();
        
        var saver = FindAnyObjectByType<TransformSaver>();
        if (saver != null)
        {
            saver.SaveCurrentTransforms();
        }
    }

    [Serializable]
    private struct InstanceInfo
    {
        public ushort prefabId;
        public GameObject source;
        public Vector3 position;
        public Quaternion rotation;
        public bool usesRotation;
        public bool includeInBounds;
    }

    [Serializable]
    private struct LevelObjectRecord
    {
        public ushort prefabId;
        public ushort x;
        public ushort z;
        public short yCm;
        public byte rot;
        public bool hasColor;
        public Color32 color;
        public bool hasExplosionEffect;
        public ushort explosionEffectId;
    }

    [Serializable]
    private struct FloorOverrideRecord
    {
        public ushort prefabId;
        public ushort x;
        public ushort z;
    }

    [Serializable]
    private struct BombDetonatorLinkRecord
    {
        public ushort detonatorCellIndex;
        public ushort bombCellIndex;
    }

    [Serializable]
    private struct PlayerRecord
    {
        public short x;
        public short z;
        public short yCm;
        public byte rot;
    }

    [Serializable]
    private struct LevelPayload
    {
        public short originX;
        public short originZ;
        public ushort width;
        public ushort height;
        public ushort baseFloorPrefabId;
        public short groundYcm;
        public byte[] floorBitmap;
        public List<FloorOverrideRecord> floorOverrides;
        public PlayerRecord player;
        public List<LevelObjectRecord> objects;
        public List<BombDetonatorLinkRecord> bombLinks;
    }

    private readonly struct GridCell : IEquatable<GridCell>
    {
        public readonly short x;
        public readonly short z;

        public GridCell(short x, short z)
        {
            this.x = x;
            this.z = z;
        }

        public bool Equals(GridCell other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is GridCell other && Equals(other);
        public override int GetHashCode() => (x << 16) ^ (ushort)z;
    }

    private struct GroundInfo
    {
        public HashSet<GridCell> cells;
        public Dictionary<GridCell, ushort> overrides;
        public short groundYcm;
    }
}
