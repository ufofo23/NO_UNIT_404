using System.Collections.Generic;
using UnityEngine;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// The building's surfaces, generated rather than shipped: plaster, the two-tone corridor
    /// paint every 1990s 복도식 block has, terrazzo, linoleum, raw concrete, and the night
    /// outside the windows.
    ///
    /// Semi-real rather than illustrated. Every surface is three fields at once - colour,
    /// gloss and height - and the height becomes a normal map, because what made the first
    /// pass look drawn was that light could not tell a grout line from a painted one. Gloss
    /// varies across a surface the way it does on a real floor (polished where nobody walks,
    /// dull where everybody does), and the colour carries grime, stains, chips and scuffs.
    ///
    /// Every cube gets UVs in metres (<see cref="MetricCube"/>), so a tile is the same size
    /// everywhere, and props get a small chamfer so their edges catch the light. Albedo stays
    /// near the greybox values on purpose: the dark is part of the design.
    ///
    /// The surfaces the map reference sheets describe - the two-tone corridor paint, the
    /// cracked wet tiles, stained ceiling board, car park concrete, rusted steel, hazard
    /// paint, plywood and plastic sheet - are painted tiles (Tools/GenerateReferenceTiles.py,
    /// Resources/NO404/Surfaces) rather than drawn here. When a tile is missing the surface
    /// falls back to its generator, so the building is never untextured.
    /// </summary>
    public static class SurfaceArt
    {
        public enum Surface
        {
            Grain, Plaster, CorridorWall, StairWall, LobbyWall, Terrazzo, Linoleum, Concrete,
            Epoxy, Tile, Ceiling, Metal, Wood, Wallpaper, Jangpan, DoorPaint, CityNight, Street,
            // Only ever painted tiles; their generators are stand-ins.
            PeelingPaint, Mould, Hazard, Plywood, Plastic
        }

        /// <summary>Turns texturing off, so tests and tools see the plain greybox.</summary>
        public static bool Enabled { get; set; } = true;

        public const string UnlitResourcePath = "NO404/Render/GreyboxUnlit";
        /// <summary>Lit with normal map and gloss-in-alpha enabled, so builds keep that variant.</summary>
        public const string SurfaceResourcePath = "NO404/Render/GreyboxSurface";
        /// <summary>Where the painted tiles are: &lt;name&gt;_albedo (rgb, a = smoothness) and &lt;name&gt;_normal.</summary>
        public const string TileResourceFolder = "NO404/Surfaces/";

        /// <summary>The chamfer a prop's edges get, in metres.</summary>
        public const float PropBevel = 0.012f;

        struct Maps
        {
            public Texture2D Albedo;   // rgb colour, a gloss
            public Texture2D Normal;
        }

        static readonly Dictionary<Surface, Maps> _maps = new Dictionary<Surface, Maps>();
        static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        static Material _unlitBase, _surfaceBase;
        static bool _unlitLooked, _surfaceLooked;

        /// <summary>
        /// The painted tile a surface uses and the metres one repeat of it covers. The metres
        /// are the ones Tools/GenerateReferenceTiles.py drew it at (its META table); change
        /// both together.
        /// </summary>
        static bool PaintedTile(Surface surface, out string name, out Vector2 metres)
        {
            switch (surface)
            {
                case Surface.CorridorWall:
                case Surface.StairWall:
                case Surface.LobbyWall: name = "wall_two_tone"; metres = new Vector2(2.6f, 2.6f); return true;
                case Surface.Terrazzo:
                case Surface.Linoleum: name = "floor_tile_cracked"; metres = new Vector2(2.4f, 2.4f); return true;
                case Surface.Ceiling: name = "ceiling_panel_stained"; metres = new Vector2(2.4f, 2.4f); return true;
                case Surface.Concrete: name = "concrete_stained"; metres = new Vector2(2.4f, 2.4f); return true;
                case Surface.Epoxy: name = "concrete_floor_wet"; metres = new Vector2(3f, 3f); return true;
                case Surface.Metal: name = "metal_rusted"; metres = Vector2.one; return true;
                case Surface.DoorPaint: name = "door_steel_painted"; metres = Vector2.one; return true;
                case Surface.Tile: name = "bathroom_tile"; metres = new Vector2(0.6f, 0.6f); return true;
                case Surface.Plaster:
                case Surface.Mould: name = "wall_mould_stain"; metres = new Vector2(2f, 2f); return true;
                case Surface.PeelingPaint: name = "wall_peeling_paint"; metres = Vector2.one; return true;
                case Surface.Hazard: name = "hazard_stripe"; metres = Vector2.one; return true;
                case Surface.Plywood: name = "plywood_old"; metres = new Vector2(1.2f, 1.2f); return true;
                case Surface.Plastic: name = "plastic_sheet"; metres = new Vector2(2f, 2f); return true;
                default: name = null; metres = Vector2.one; return false;
            }
        }

        static readonly Dictionary<Surface, bool> _painted = new Dictionary<Surface, bool>();

        /// <summary>True when the surface's painted tile is in the build.</summary>
        static bool HasPaintedTile(Surface surface)
        {
            bool has;
            if (_painted.TryGetValue(surface, out has)) return has;
            string name; Vector2 metres;
            has = PaintedTile(surface, out name, out metres) &&
                  Resources.Load<Texture2D>(TileResourceFolder + name + "_albedo") != null;
            _painted[surface] = has;
            return has;
        }

        /// <summary>How many metres one repeat of a surface covers, (u, v).</summary>
        public static Vector2 TileOf(Surface surface)
        {
            string painted; Vector2 metres;
            if (PaintedTile(surface, out painted, out metres) && HasPaintedTile(surface)) return metres;

            switch (surface)
            {
                case Surface.CorridorWall:
                case Surface.StairWall:
                case Surface.LobbyWall: return new Vector2(BuildingSpec.WallHeight, BuildingSpec.WallHeight);
                case Surface.Terrazzo: return new Vector2(1.2f, 1.2f);
                case Surface.Linoleum: return new Vector2(1.2f, 1.2f);
                case Surface.Concrete: return new Vector2(2.4f, 2.4f);
                case Surface.Epoxy: return new Vector2(3f, 3f);
                case Surface.Tile: return new Vector2(0.6f, 0.6f);
                case Surface.Ceiling: return new Vector2(1.2f, 1.2f);
                case Surface.Plaster: return new Vector2(2f, 2f);
                case Surface.Wallpaper: return new Vector2(2f, 2f);
                default: return Vector2.one;
            }
        }

        /// <summary>Dresses a box built by the greybox. Does nothing when art is off.</summary>
        public static void Apply(GameObject box, Vector3 size, Surface surface, Color tint, float bevel = 0f)
        {
            if (!Enabled || box == null) return;
            var filter = box.GetComponent<MeshFilter>();
            var renderer = box.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) return;

            var tile = TileOf(surface);
            filter.sharedMesh = MetricCube(size, tile.x, tile.y, bevel);
            renderer.sharedMaterial = Lit(surface, tint);
        }

        /// <summary>A view out of the building: texture stretched once across the whole box.</summary>
        public static void ApplyView(GameObject box, Vector3 size, Surface surface, float brightness)
        {
            if (!Enabled || box == null) return;
            var filter = box.GetComponent<MeshFilter>();
            var renderer = box.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) return;

            filter.sharedMesh = MetricCube(size, Mathf.Max(size.x, size.z), size.y, 0f);
            var tint = Color.white * brightness;
            tint.a = 1f;
            renderer.sharedMaterial = Unlit(surface, tint);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A surface that is its own light: a tube, an exit sign. HDR, so it blooms.</summary>
        public static void ApplyGlow(GameObject box, Color colour)
        {
            if (box == null) return;
            var renderer = box.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = Unlit(Surface.Grain, colour, untextured: true);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // -----------------------------------------------------------------
        // materials
        // -----------------------------------------------------------------

        public static Material Lit(Surface surface, Color tint)
        {
            string key = "L" + (int)surface + ColorUtility.ToHtmlStringRGBA(tint);
            Material material;
            if (_materials.TryGetValue(key, out material) && material != null) return material;

            if (!_surfaceLooked)
            {
                _surfaceLooked = true;
                _surfaceBase = Resources.Load<Material>(SurfaceResourcePath);
                if (_surfaceBase == null) Log.Warn("Render", "no surface material at Resources/" + SurfaceResourcePath);
            }

            if (_surfaceBase != null)
            {
                material = new Material(_surfaceBase);
                SetColour(material, tint);
            }
            else
            {
                // Editor without the asset: the keywords still work there.
                material = GreyboxMaterial.Tinted(tint);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 1f);
            }

            var maps = MapsOf(surface);
            material.name = "SURFACE_" + surface + "_" + ColorUtility.ToHtmlStringRGB(tint);
            SetTexture(material, maps.Albedo);
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", maps.Normal);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f);
            // Painted steel with rust through it is mostly not bare metal.
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", surface == Surface.Metal ? 0.35f : 0f);
            _materials[key] = material;
            return material;
        }

        static Material Unlit(Surface surface, Color tint, bool untextured = false)
        {
            string key = "U" + (untextured ? "-" : ((int)surface).ToString()) + tint.r.ToString("0.00") +
                         tint.g.ToString("0.00") + tint.b.ToString("0.00");
            Material material;
            if (_materials.TryGetValue(key, out material) && material != null) return material;

            if (!_unlitLooked)
            {
                _unlitLooked = true;
                _unlitBase = Resources.Load<Material>(UnlitResourcePath);
                if (_unlitBase == null) Log.Warn("Render", "no unlit material at Resources/" + UnlitResourcePath);
            }

            // Without the unlit asset a lit copy is the honest fallback: a pale panel rather
            // than something that vanishes into the dark.
            material = _unlitBase != null ? new Material(_unlitBase) : GreyboxMaterial.Tinted(tint);
            material.name = "GLOW_" + surface;
            SetColour(material, tint);
            if (!untextured) SetTexture(material, MapsOf(surface).Albedo);
            _materials[key] = material;
            return material;
        }

        static void SetColour(Material material, Color tint)
        {
            if (material.HasProperty(Interaction.ShaderIds.BaseColor)) material.SetColor(Interaction.ShaderIds.BaseColor, tint);
            material.color = tint;
        }

        static void SetTexture(Material material, Texture2D texture)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }

        // -----------------------------------------------------------------
        // meshes
        // -----------------------------------------------------------------

        /// <summary>
        /// A unit cube whose UVs are measured in metres of the box it will be scaled to, so a
        /// texture repeats every <paramref name="tileU"/> by <paramref name="tileV"/> metres
        /// whatever the box. Vertical faces put v = 0 at the bottom edge, which is what lets a
        /// wall texture paint its dado at the right height.
        ///
        /// <paramref name="bevel"/> chamfers every edge by that many metres. The chamfer is
        /// built in metres and divided back into unit space, so it stays the same width on a
        /// door and on a desk; the collider is the primitive's own box and does not change.
        /// </summary>
        public static Mesh MetricCube(Vector3 size, float tileU, float tileV, float bevel = 0f)
        {
            float minDim = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float b = Mathf.Min(bevel, minDim * 0.25f);
            if (b < 0.002f) b = 0f;

            string key = size.x.ToString("0.000") + "," + size.y.ToString("0.000") + "," + size.z.ToString("0.000") +
                         "/" + tileU.ToString("0.000") + "," + tileV.ToString("0.000") + "/" + b.ToString("0.000");
            Mesh mesh;
            if (_meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;

            var builder = new CubeBuilder(size, tileU, tileV, b);
            mesh = builder.Build("MetricCube_" + key);
            _meshes[key] = mesh;
            return mesh;
        }

        sealed class CubeBuilder
        {
            readonly Vector3 _size, _half;
            readonly float _tileU, _tileV, _b;
            readonly List<Vector3> _v = new List<Vector3>(96);
            readonly List<Vector3> _n = new List<Vector3>(96);
            readonly List<Vector2> _uv = new List<Vector2>(96);
            readonly List<int> _t = new List<int>(180);

            public CubeBuilder(Vector3 size, float tileU, float tileV, float bevel)
            {
                _size = size; _half = size * 0.5f; _tileU = tileU; _tileV = tileV; _b = bevel;
            }

            public Mesh Build(string name)
            {
                // Six faces, each inset by the bevel on its two in-plane axes.
                Face(Vector3.forward, Vector3.left, Vector3.up, _tileU, _tileV);
                Face(Vector3.back, Vector3.right, Vector3.up, _tileU, _tileV);
                Face(Vector3.right, Vector3.forward, Vector3.up, _tileU, _tileV);
                Face(Vector3.left, Vector3.back, Vector3.up, _tileU, _tileV);
                Face(Vector3.up, Vector3.right, Vector3.forward, _tileU, _tileU);
                Face(Vector3.down, Vector3.right, Vector3.back, _tileU, _tileU);

                if (_b > 0f)
                {
                    // Twelve chamfer strips, one per edge, and eight corner triangles.
                    for (int a = 0; a < 3; a++)
                        for (int c = a + 1; c < 3; c++)
                            for (int sa = -1; sa <= 1; sa += 2)
                                for (int sc = -1; sc <= 1; sc += 2)
                                    Edge(a, sa, c, sc);

                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sy = -1; sy <= 1; sy += 2)
                            for (int sz = -1; sz <= 1; sz += 2)
                                Corner(sx, sy, sz);
                }

                // Into unit space: the transform scales back by the size. Normals go the
                // other way (inverse-transpose), so they are pre-scaled by the size.
                for (int i = 0; i < _v.Count; i++)
                {
                    var p = _v[i];
                    _v[i] = new Vector3(p.x / _size.x, p.y / _size.y, p.z / _size.z);
                    var n = _n[i];
                    _n[i] = new Vector3(n.x * _size.x, n.y * _size.y, n.z * _size.z).normalized;
                }

                var mesh = new Mesh { name = name };
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }

            static float Comp(Vector3 v, int axis) { return axis == 0 ? v.x : axis == 1 ? v.y : v.z; }

            static Vector3 Axis(int axis, float value)
            {
                return axis == 0 ? new Vector3(value, 0f, 0f) : axis == 1 ? new Vector3(0f, value, 0f) : new Vector3(0f, 0f, value);
            }

            void Face(Vector3 normal, Vector3 uAxis, Vector3 vAxis, float tileU, float tileV)
            {
                float uHalf = Mathf.Abs(Vector3.Dot(uAxis, _half));
                float vHalf = Mathf.Abs(Vector3.Dot(vAxis, _half));
                float depth = Mathf.Abs(Vector3.Dot(normal, _half));
                float ui = uHalf - _b, vi = vHalf - _b;

                int start = _v.Count;
                for (int i = 0; i < 4; i++)
                {
                    float a = (i == 1 || i == 2) ? 1f : -1f;
                    float c = (i >= 2) ? 1f : -1f;
                    _v.Add(normal * depth + uAxis * (a * ui) + vAxis * (c * vi));
                    _n.Add(normal);
                    // Metric UV measured from the face's own corner, so walls start at the floor.
                    _uv.Add(new Vector2((a * ui + uHalf) / Mathf.Max(0.01f, tileU), (c * vi + vHalf) / Mathf.Max(0.01f, tileV)));
                }
                _t.Add(start); _t.Add(start + 2); _t.Add(start + 1);
                _t.Add(start); _t.Add(start + 3); _t.Add(start + 2);
            }

            void Edge(int a, int sa, int c, int sc)
            {
                int along = 3 - a - c;
                float ha = Comp(_half, a), hc = Comp(_half, c), hl = Comp(_half, along) - _b;
                var n = (Axis(a, sa) + Axis(c, sc)).normalized;

                // The edge of face a (inset on c) and the edge of face c (inset on a).
                var p0 = Axis(a, sa * ha) + Axis(c, sc * (hc - _b));
                var p1 = Axis(a, sa * (ha - _b)) + Axis(c, sc * hc);
                var e0 = Axis(along, -hl);
                var e1 = Axis(along, hl);

                Quad(p0 + e0, p0 + e1, p1 + e1, p1 + e0, n, along);
            }

            void Corner(int sx, int sy, int sz)
            {
                var p = new Vector3(sx * _half.x, sy * _half.y, sz * _half.z);
                var x = new Vector3(p.x, sy * (_half.y - _b), sz * (_half.z - _b));
                var y = new Vector3(sx * (_half.x - _b), p.y, sz * (_half.z - _b));
                var z = new Vector3(sx * (_half.x - _b), sy * (_half.y - _b), p.z);
                var n = new Vector3(sx, sy, sz).normalized;
                Triangle(x, y, z, n);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, int along)
            {
                Triangle(a, b, c, n, along);
                Triangle(a, c, d, n, along);
            }

            void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 n, int along = 1)
            {
                // Convex and centred, so outward is away from the origin.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f) { var t = b; b = c; c = t; }
                int start = _v.Count;
                foreach (var p in new[] { a, b, c })
                {
                    _v.Add(p);
                    _n.Add(n);
                    _uv.Add(new Vector2(Comp(p, along) / Mathf.Max(0.01f, _tileU), (p.x + p.y + p.z) / Mathf.Max(0.01f, _tileV)));
                }
                _t.Add(start); _t.Add(start + 1); _t.Add(start + 2);
            }
        }

        // -----------------------------------------------------------------
        // textures
        // -----------------------------------------------------------------

        public static Texture2D Texture(Surface surface) { return MapsOf(surface).Albedo; }

        public static Texture2D NormalMap(Surface surface) { return MapsOf(surface).Normal; }

        static Maps MapsOf(Surface surface)
        {
            Maps maps;
            if (_maps.TryGetValue(surface, out maps) && maps.Albedo != null) return maps;

            string painted; Vector2 metres;
            if (PaintedTile(surface, out painted, out metres) && HasPaintedTile(surface))
            {
                maps.Albedo = Resources.Load<Texture2D>(TileResourceFolder + painted + "_albedo");
                maps.Normal = Resources.Load<Texture2D>(TileResourceFolder + painted + "_normal");
                if (maps.Normal != null)
                {
                    _maps[surface] = maps;
                    return maps;
                }
                Log.Warn("Render", "painted tile " + painted + " has no normal map; drawing it instead");
            }

            var field = Generate(surface);
            bool clamp = surface == Surface.CityNight || surface == Surface.Street;

            maps.Albedo = new Texture2D(field.W, field.H, TextureFormat.RGBA32, true);
            maps.Albedo.name = "SURF_" + surface;
            var px = new Color[field.W * field.H];
            for (int i = 0; i < px.Length; i++)
            {
                var c = field.Colour[i];
                c.a = Mathf.Clamp01(field.Gloss[i]);
                px[i] = c;
            }
            maps.Albedo.SetPixels(px);
            Finish(maps.Albedo, clamp);

            maps.Normal = NormalFromHeight(field, surface.ToString());
            Finish(maps.Normal, clamp);

            _maps[surface] = maps;
            return maps;
        }

        static void Finish(Texture2D texture, bool clamp)
        {
            texture.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 8;
            texture.Apply(true, true);
        }

        /// <summary>
        /// Normal map from the height field, wrapping at the edges so tiling stays seamless.
        /// Stored as (x, y, z, 1): read either as RGB or as the AG-swizzled form, x comes out
        /// right, which is what keeps it correct on every platform's normal encoding.
        /// </summary>
        static Texture2D NormalFromHeight(Field f, string name)
        {
            var tex = new Texture2D(f.W, f.H, TextureFormat.RGBA32, true, true) { name = "NRM_" + name };
            var px = new Color[f.W * f.H];
            for (int y = 0; y < f.H; y++)
                for (int x = 0; x < f.W; x++)
                {
                    float l = f.Height[y * f.W + (x + f.W - 1) % f.W];
                    float r = f.Height[y * f.W + (x + 1) % f.W];
                    float d = f.Height[((y + f.H - 1) % f.H) * f.W + x];
                    float u = f.Height[((y + 1) % f.H) * f.W + x];
                    var n = new Vector3((l - r) * f.Bump, (d - u) * f.Bump, 1f).normalized;
                    px[y * f.W + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            tex.SetPixels(px);
            return tex;
        }

        /// <summary>Colour, gloss and height for one surface, plus how hard to read the height.</summary>
        sealed class Field
        {
            public readonly int W, H;
            public readonly Color[] Colour;
            public readonly float[] Gloss, Height;
            public float Bump = 2f;

            public Field(int w, int h)
            {
                W = w; H = h;
                Colour = new Color[w * h];
                Gloss = new float[w * h];
                Height = new float[w * h];
            }

            public void Set(int x, int y, Color c, float gloss, float height)
            {
                int i = y * W + x;
                c.a = 1f;
                Colour[i] = c; Gloss[i] = gloss; Height[i] = height;
            }
        }

        static Field Generate(Surface surface)
        {
            switch (surface)
            {
                case Surface.Plaster: return Plaster();
                case Surface.CorridorWall: return PaintedDado(new Color(0.20f, 0.28f, 0.24f), new Color(0.60f, 0.585f, 0.52f), 0.40f, 21);
                case Surface.StairWall: return PaintedDado(new Color(0.30f, 0.24f, 0.20f), new Color(0.56f, 0.55f, 0.50f), 0.38f, 23);
                case Surface.LobbyWall: return TiledDado();
                case Surface.Terrazzo: return Terrazzo();
                case Surface.Linoleum: return Linoleum();
                case Surface.Concrete: return Concrete(512, 31, new Color(0.45f, 0.45f, 0.44f));
                case Surface.Epoxy: return Epoxy();
                case Surface.Tile: return Tile();
                case Surface.Ceiling: return CeilingTiles();
                case Surface.Metal: return Brushed();
                case Surface.Wood: return WoodGrain();
                case Surface.Wallpaper: return Wallpaper();
                case Surface.Jangpan: return Jangpan();
                case Surface.DoorPaint: return DoorPaint();
                case Surface.CityNight: return CityNight();
                case Surface.Street: return Street();
                case Surface.PeelingPaint: return PaintedDado(new Color(0.20f, 0.40f, 0.46f), new Color(0.20f, 0.40f, 0.46f), 0.40f, 27);
                case Surface.Mould: return Plaster();
                case Surface.Hazard: return DoorPaint();
                case Surface.Plywood: return WoodGrain();
                case Surface.Plastic: return Plaster();
                default: return Grain();
            }
        }

        // ---- noise ------------------------------------------------------------

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Tileable value noise; the lattice wraps at its periods.</summary>
        static float Value(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = ((x0 % px) + px) % px, bx = (ax + 1) % px;
            int ay = ((y0 % py) + py) % py, by = (ay + 1) % py;
            float a = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
            float b = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        /// <summary>Four octaves of tileable noise, 0..1. Cells can differ per axis for streaks.</summary>
        static float Fbm(int x, int y, int w, int h, int cellsX, int cellsY, int seed)
        {
            float sum = 0f, amp = 0.5f;
            for (int o = 0; o < 4; o++)
            {
                sum += amp * Value(x * cellsX / (float)w, y * cellsY / (float)h, cellsX, cellsY, seed + o * 101);
                cellsX *= 2; cellsY *= 2; amp *= 0.5f;
            }
            return sum / 0.9375f;
        }

        static float Fbm(int x, int y, int s, int cells, int seed) { return Fbm(x, y, s, s, cells, cells, seed); }

        static float Smooth(float a, float b, float v) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v)); }

        static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

        /// <summary>
        /// Distance to the nearest jittered point in a tileable grid, and that point's id.
        /// The basis of chips, pits and speckles.
        /// </summary>
        static float Nearest(int x, int y, int s, int cell, int seed, out int id)
        {
            int cells = Mathf.Max(1, s / cell);
            int cx = x / cell, cy = y / cell;
            float best = float.MaxValue; id = 0;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    int wx = ((gx % cells) + cells) % cells, wy = ((gy % cells) + cells) % cells;
                    float jx = (gx + Hash(wx, wy, seed)) * cell;
                    float jy = (gy + Hash(wx, wy, seed + 1)) * cell;
                    float dx = x - jx, dy = y - jy;
                    float d = dx * dx + dy * dy;
                    if (d < best) { best = d; id = wy * cells + wx; }
                }
            return Mathf.Sqrt(best);
        }

        /// <summary>Dirt that collects at the bottom of a wall texture whose v is height.</summary>
        static float FloorGrime(float v, float fbm) { return Mathf.Clamp01(1f - v / 0.16f) * (0.10f + 0.12f * fbm); }

        // ---- surfaces ---------------------------------------------------------

        static Field Grain()
        {
            const int S = 256;
            var f = new Field(S, S) { Bump = 0.6f };
            var baseC = new Color(0.92f, 0.92f, 0.91f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Fbm(x, y, S, 6, 7), m = Fbm(x, y, S, 32, 9);
                    float scratch = Smooth(0.86f, 0.9f, Fbm(x, y, S, S, 64, 2, 13)) * 0.5f;
                    var c = Mul(baseC, 0.9f + 0.12f * n - scratch * 0.08f);
                    f.Set(x, y, c, 0.22f + 0.12f * m - scratch * 0.1f, m * 0.4f + scratch * 0.3f);
                }
            return f;
        }

        static Field Plaster()
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 0.35f };
            var baseC = new Color(0.60f, 0.585f, 0.55f);
            var stainC = new Color(0.44f, 0.39f, 0.31f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float peel = Fbm(x, y, S, 96, 11);
                    float broad = Fbm(x, y, S, 6, 12);
                    float stain = Smooth(0.66f, 0.82f, Fbm(x, y, S, 3, 13)) * 0.16f;
                    var c = Color.Lerp(Mul(baseC, 0.92f + 0.12f * broad), stainC, stain);
                    f.Set(x, y, c, 0.16f + 0.08f * peel - stain * 0.08f, peel * 0.6f + broad * 0.2f);
                }
            return f;
        }

        /// <summary>
        /// The corridor paint: gloss enamel to waist height, a thin line, matte emulsion above.
        /// The enamel is chipped where trolleys and shoes have hit it for thirty years, the
        /// chips show primer and sit lower, and dirt gathers along the skirting.
        /// </summary>
        static Field PaintedDado(Color lower, Color upper, float split, int seed)
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 0.7f };
            var primer = new Color(0.50f, 0.49f, 0.46f);
            var skirting = new Color(0.11f, 0.11f, 0.11f);
            for (int y = 0; y < S; y++)
            {
                float v = y / (float)S;
                for (int x = 0; x < S; x++)
                {
                    float roller = Fbm(x, y, S, 80, seed);
                    float broad = Fbm(x, y, S, 5, seed + 1);
                    Color c; float gloss, height;

                    if (v < 0.035f)
                    {
                        c = Mul(skirting, 0.9f + 0.2f * broad); gloss = 0.32f; height = 0.6f;
                    }
                    else if (v < split)
                    {
                        // Chips are likeliest near the floor and along the top edge of the band.
                        float bias = Mathf.Max(Mathf.Clamp01(1f - v / 0.2f), Smooth(split - 0.05f, split, v)) * 0.08f;
                        float chip = Smooth(0.80f - bias, 0.83f - bias, Fbm(x, y, S, 28, seed + 3));
                        float scuff = Smooth(0.72f, 0.9f, Fbm(x, y, S, S, 6, 48, seed + 4)) * Mathf.Clamp01(1f - v / 0.3f);
                        c = Color.Lerp(Mul(lower, 0.94f + 0.1f * broad - scuff * 0.25f), primer, chip);
                        gloss = Mathf.Lerp(0.55f - scuff * 0.25f, 0.12f, chip);
                        height = 0.5f + roller * 0.15f - chip * 0.5f;
                    }
                    else if (v < split + 0.012f)
                    {
                        c = Mul(lower, 0.55f); gloss = 0.5f; height = 0.55f;
                    }
                    else
                    {
                        float drip = Smooth(0.82f, 0.9f, Fbm(x, y, S, S, 48, 3, seed + 6)) * Smooth(0.5f, 0.9f, v) * 0.18f;
                        c = Mul(upper, 0.94f + 0.1f * broad - drip);
                        gloss = 0.17f + 0.05f * roller;
                        height = 0.45f + roller * 0.2f;
                    }

                    float grime = FloorGrime(v, Fbm(x, y, S, 24, seed + 7));
                    if (v > 0.93f) grime += (v - 0.93f) * 1.6f * Fbm(x, y, S, 12, seed + 8);
                    f.Set(x, y, Mul(c, 1f - grime), gloss * (1f - grime), height);
                }
            }
            return f;
        }

        /// <summary>The lobby: small glazed tiles to shoulder height, paint above.</summary>
        static Field TiledDado()
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 1.6f };
            var tile = new Color(0.68f, 0.68f, 0.65f);
            var grout = new Color(0.36f, 0.35f, 0.32f);
            var upper = new Color(0.60f, 0.585f, 0.54f);
            const float split = 0.46f;
            int cell = S / 26;   // ~10cm tiles on a 2.6m repeat
            for (int y = 0; y < S; y++)
            {
                float v = y / (float)S;
                for (int x = 0; x < S; x++)
                {
                    Color c; float gloss, height;
                    if (v < split)
                    {
                        int tx = x % cell, ty = y % cell;
                        bool g = tx < 2 || ty < 2;
                        float edge = Mathf.Min(Mathf.Min(tx, cell - tx), Mathf.Min(ty, cell - ty)) / 3f;
                        float tone = 0.94f + 0.08f * Hash(x / cell, y / cell, 61);
                        c = g ? Mul(grout, 0.9f + 0.2f * Fbm(x, y, S, 40, 62)) : Mul(tile, tone);
                        gloss = g ? 0.08f : 0.72f;
                        height = g ? 0f : Mathf.Clamp01(edge) * 0.9f + 0.1f;
                    }
                    else if (v < split + 0.015f)
                    {
                        c = Mul(tile, 0.55f); gloss = 0.6f; height = 0.9f;
                    }
                    else
                    {
                        float roller = Fbm(x, y, S, 80, 63);
                        c = Mul(upper, 0.95f + 0.08f * Fbm(x, y, S, 6, 64)); gloss = 0.16f; height = 0.4f + roller * 0.2f;
                    }
                    float grime = FloorGrime(v, Fbm(x, y, S, 24, 65));
                    f.Set(x, y, Mul(c, 1f - grime), gloss * (1f - grime * 0.8f), height);
                }
            }
            return f;
        }

        /// <summary>
        /// Grey terrazzo in 600mm slabs: two sizes of marble chip, recessed brass-coloured
        /// joints full of dirt, and the dull walking line where wax has worn off.
        /// </summary>
        static Field Terrazzo()
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 1.2f };
            var baseC = new Color(0.45f, 0.44f, 0.42f);
            var chips = new[]
            {
                new Color(0.82f, 0.80f, 0.76f), new Color(0.14f, 0.13f, 0.13f), new Color(0.52f, 0.36f, 0.28f),
                new Color(0.38f, 0.42f, 0.40f), new Color(0.66f, 0.62f, 0.55f)
            };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Fbm(x, y, S, 8, 71);
                    var c = Mul(baseC, 0.9f + 0.15f * n);
                    float height = 0.5f;

                    int id;
                    float d = Nearest(x, y, S, 7, 72, out id);
                    if (d < 1.3f + 2.2f * Hash(id, 0, 73)) c = Mul(chips[(int)(Hash(id, 1, 74) * chips.Length) % chips.Length], 0.9f + 0.15f * Hash(id, 2, 75));
                    d = Nearest(x, y, S, 19, 76, out id);
                    if (d < 3f + 3.5f * Hash(id, 0, 77)) c = Mul(chips[(int)(Hash(id, 1, 78) * chips.Length) % chips.Length], 0.88f + 0.15f * Hash(id, 2, 79));

                    bool joint = x % (S / 2) < 2 || y % (S / 2) < 2;
                    float wear = Smooth(0.45f, 0.75f, Fbm(x, y, S, 3, 80));
                    float gloss = Mathf.Lerp(0.62f, 0.30f, wear) - 0.06f * Fbm(x, y, S, 40, 81);
                    if (joint) { c = new Color(0.30f, 0.27f, 0.20f); gloss = 0.15f; height = 0f; }
                    float crack = Smooth(0.985f, 0.995f, 1f - Mathf.Abs(Fbm(x, y, S, 5, 82) - 0.5f) * 2f) * Smooth(0.6f, 0.7f, Fbm(x, y, S, 2, 83));
                    c = Mul(c, 1f - crack * 0.5f - wear * 0.06f);
                    f.Set(x, y, c, gloss, height - crack * 0.4f);
                }
            return f;
        }

        /// <summary>Office linoleum: 300mm tiles laid in two tones, waxed, heel-marked.</summary>
        static Field Linoleum()
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 1f };
            int cell = S / 4;
            var a = new Color(0.36f, 0.40f, 0.37f);
            var b = new Color(0.33f, 0.36f, 0.34f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    bool alt = ((x / cell) + (y / cell)) % 2 == 0;
                    float marble = Fbm(x, y, S, 16, 81);
                    var c = Mul(alt ? a : b, 0.9f + 0.18f * marble);
                    int id;
                    float d = Nearest(x, y, S, 23, 84, out id);
                    float heel = Hash(id, 0, 85) < 0.25f ? Smooth(4f, 1.5f, d) * 0.45f : 0f;
                    float wear = Smooth(0.4f, 0.8f, Fbm(x, y, S, 3, 86));
                    bool seam = x % cell < 1 || y % cell < 1;
                    c = Mul(c, 1f - heel);
                    float gloss = Mathf.Lerp(0.5f, 0.28f, wear) - heel * 0.2f;
                    if (seam) { c = Mul(c, 0.6f); gloss = 0.1f; }
                    f.Set(x, y, c, gloss, seam ? 0f : 0.5f + 0.1f * marble);
                }
            return f;
        }

        static Field Concrete(int s, int seed, Color baseC)
        {
            var f = new Field(s, s) { Bump = 1.3f };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float n = Fbm(x, y, s, 8, seed);
                    float fine = Fbm(x, y, s, 64, seed + 1);
                    float damp = Smooth(0.58f, 0.75f, Fbm(x, y, s, 2, seed + 2));
                    int id;
                    float d = Nearest(x, y, s, 9, seed + 3, out id);
                    float pit = Hash(id, 0, seed + 4) < 0.04f ? Smooth(1.2f, 0.3f, d) : 0f;
                    // Formwork tie holes on a 600mm grid.
                    int q = s / 4;
                    int cx = x % q - q / 2, cy = y % q - q / 2;
                    float tie = Smooth(16f, 6f, cx * cx + cy * cy);
                    var c = Mul(baseC, 0.82f + 0.3f * n - damp * 0.18f - pit * 0.15f - tie * 0.3f);
                    float gloss = 0.08f + 0.06f * fine + damp * 0.18f;
                    f.Set(x, y, c, gloss, 0.5f + 0.25f * fine + 0.2f * n - pit * 0.3f - tie * 0.5f);
                }
            return f;
        }

        /// <summary>Car park floor paint, worn through to concrete where wheels turn, tyre-marked.</summary>
        static Field Epoxy()
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 0.9f };
            var paint = new Color(0.27f, 0.34f, 0.31f);
            var concrete = new Color(0.43f, 0.43f, 0.41f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Fbm(x, y, S, 6, 91);
                    float worn = Smooth(0.62f, 0.72f, Fbm(x, y, S, 5, 92));
                    float tyre = Smooth(0.7f, 0.86f, Fbm(x, y, S, S, 3, 40, 93)) * 0.35f;
                    var c = Color.Lerp(Mul(paint, 0.9f + 0.15f * n), Mul(concrete, 0.9f + 0.2f * n), worn);
                    c = Mul(c, 1f - tyre);
                    float fine = Fbm(x, y, S, 64, 94);
                    f.Set(x, y, c, Mathf.Lerp(0.48f, 0.12f, worn) - tyre * 0.15f, 0.5f + fine * 0.2f - worn * 0.2f);
                }
            return f;
        }

        static Field Tile()
        {
            const int S = 256;
            var f = new Field(S, S) { Bump = 2f };
            int cell = S / 4;
            var glaze = new Color(0.72f, 0.73f, 0.70f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int tx = x % cell, ty = y % cell;
                    bool g = tx < 3 || ty < 3;
                    float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(tx - 3, cell - tx), Mathf.Min(ty - 3, cell - ty)) / 4f);
                    float tone = 0.93f + 0.08f * Hash(x / cell, y / cell, 41);
                    var c = g ? Mul(new Color(0.40f, 0.39f, 0.35f), 0.85f + 0.25f * Fbm(x, y, S, 32, 42)) : Mul(glaze, tone - 0.05f * Fbm(x, y, S, 8, 43));
                    f.Set(x, y, c, g ? 0.06f : 0.78f, g ? 0f : 0.3f + edge * 0.7f);
                }
            return f;
        }

        /// <summary>Mineral-fibre ceiling tiles: fissures, pinholes, a T-bar grid, the odd leak.</summary>
        static Field CeilingTiles()
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 0.9f };
            var fill = new Color(0.60f, 0.59f, 0.56f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float fissure = Smooth(0.62f, 0.7f, Fbm(x, y, S, 48, 95));
                    int id;
                    float d = Nearest(x, y, S, 6, 96, out id);
                    float pin = Smooth(1.2f, 0.3f, d) * (Hash(id, 0, 97) < 0.6f ? 1f : 0f);
                    var c = Mul(fill, 0.96f + 0.06f * Fbm(x, y, S, 6, 98) - fissure * 0.06f - pin * 0.1f);
                    float height = 0.5f - fissure * 0.3f - pin * 0.4f;
                    float gloss = 0.05f;
                    bool bar = x % (S / 2) < 4 || y % (S / 2) < 4;
                    if (bar) { c = new Color(0.50f, 0.50f, 0.49f); height = 1f; gloss = 0.45f; }
                    float leak = Smooth(0.68f, 0.8f, Fbm(x, y, S, 2, 99));
                    c = Color.Lerp(c, new Color(0.46f, 0.40f, 0.29f), leak * 0.6f);
                    f.Set(x, y, c, gloss, height);
                }
            return f;
        }

        static Field Brushed()
        {
            const int S = 256;
            var f = new Field(S, S) { Bump = 0.8f };
            var baseC = new Color(0.58f, 0.59f, 0.60f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float streak = Fbm(x, y, S, S, 2, 128, 101);
                    float smudge = Smooth(0.55f, 0.75f, Fbm(x, y, S, 4, 102));
                    var c = Mul(baseC, 0.9f + 0.15f * streak - smudge * 0.05f);
                    f.Set(x, y, c, 0.62f - smudge * 0.25f + streak * 0.08f, streak * 0.5f);
                }
            return f;
        }

        static Field WoodGrain()
        {
            const int S = 256;
            var f = new Field(S, S) { Bump = 1.5f };
            var a = new Color(0.38f, 0.26f, 0.16f);
            var b = new Color(0.27f, 0.18f, 0.11f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float warp = Fbm(x, y, S, S, 2, 6, 111) * 5f;
                    float ring = Mathf.Abs(Mathf.Sin((y / (float)S * 9f + warp) * Mathf.PI));
                    float pore = Fbm(x, y, S, S, 128, 8, 112);
                    var c = Color.Lerp(b, a, ring * 0.75f + 0.25f * pore);
                    f.Set(x, y, c, 0.35f + 0.1f * ring, ring * 0.3f + pore * 0.3f);
                }
            return f;
        }

        /// <summary>
        /// Unit 404: embossed vinyl wallpaper of the kind every 1990s flat had, gone the colour
        /// of weak tea, with a tide line where water once came down the wall.
        /// </summary>
        static Field Wallpaper()
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 1f };
            var paper = new Color(0.58f, 0.53f, 0.43f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // A small lozenge emboss, mostly felt rather than seen.
                    float u = x / 32f, v = y / 32f;
                    float diamond = Mathf.Abs(Mathf.Sin(u * Mathf.PI + v * Mathf.PI)) * Mathf.Abs(Mathf.Sin(u * Mathf.PI - v * Mathf.PI));
                    float fibre = Fbm(x, y, S, 96, 121);
                    float broad = Fbm(x, y, S, 4, 122);
                    float tide = Fbm(x, y, S, 2, 123);
                    float stain = Smooth(0.6f, 0.75f, tide);
                    float line = Smooth(0.03f, 0f, Mathf.Abs(tide - 0.62f)) * 0.5f;
                    var c = Mul(paper, 0.92f + 0.1f * broad + 0.03f * diamond);
                    c = Color.Lerp(c, new Color(0.45f, 0.38f, 0.27f), stain * 0.14f + line * 0.12f);
                    int id;
                    float d = Nearest(x, y, S, 11, 124, out id);
                    float mould = Hash(id, 0, 125) < 0.1f ? Smooth(2.2f, 0.6f, d) * stain : 0f;
                    c = Mul(c, 1f - mould * 0.3f);
                    f.Set(x, y, c, 0.22f + 0.08f * diamond - stain * 0.08f, diamond * 0.6f + fibre * 0.3f);
                }
            return f;
        }

        /// <summary>장판: yellowed vinyl floor sheet with a faint printed weave and a few burns.</summary>
        static Field Jangpan()
        {
            const int S = 512;
            var f = new Field(S, S) { Bump = 0.7f };
            var baseC = new Color(0.62f, 0.49f, 0.29f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float weave = Fbm(x, y, S, S, 96, 6, 131) * 0.5f + Fbm(x, y, S, S, 6, 96, 132) * 0.5f;
                    float broad = Fbm(x, y, S, 4, 133);
                    int id;
                    float d = Nearest(x, y, S, 64, 134, out id);
                    float burn = Hash(id, 0, 135) < 0.15f ? Smooth(9f, 2f, d) : 0f;
                    var c = Mul(baseC, 0.88f + 0.12f * weave + 0.08f * broad);
                    c = Color.Lerp(c, new Color(0.18f, 0.12f, 0.07f), burn * 0.8f);
                    f.Set(x, y, c, 0.48f - burn * 0.3f - broad * 0.1f, weave * 0.4f - burn * 0.3f);
                }
            return f;
        }

        static Field DoorPaint()
        {
            const int S = 256;
            var f = new Field(S, S) { Bump = 0.6f };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float peel = Fbm(x, y, S, 48, 141);
                    float scuff = Smooth(0.75f, 0.85f, Fbm(x, y, S, 16, 142));
                    var c = Mul(new Color(0.82f, 0.82f, 0.82f), 0.92f + 0.08f * Fbm(x, y, S, 5, 143) - scuff * 0.15f);
                    f.Set(x, y, c, 0.40f - scuff * 0.2f + 0.05f * peel, peel * 0.5f - scuff * 0.2f);
                }
            return f;
        }

        // ---- views ------------------------------------------------------------

        /// <summary>
        /// The view from a corridor at night, as a lens would see it focused on the glass: a
        /// sodium haze over the horizon, two rows of blocks, everyone else's windows, and the
        /// street lamps below blown into soft discs.
        /// </summary>
        static Field CityNight()
        {
            const int W = 1024, H = 512;
            var f = new Field(W, H) { Bump = 0f };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)H;
                    var sky = Color.Lerp(new Color(0.20f, 0.12f, 0.08f), new Color(0.015f, 0.02f, 0.04f), Mathf.Pow(v, 0.55f));
                    px[y * W + x] = sky;
                }

            Blocks(px, W, H, 211, 0.34f, 0.64f, new Color(0.075f, 0.065f, 0.07f), 0.10f, 0.55f);
            Blocks(px, W, H, 223, 0.12f, 0.46f, new Color(0.03f, 0.03f, 0.035f), 0.20f, 1f);
            Blur(px, W, H, 1);

            // Street lamps and cars below the sill line, out of focus.
            for (int i = 0; i < 22; i++)
            {
                float cx = Hash(i, 0, 230) * W, cy = Hash(i, 1, 230) * H * 0.12f;
                float r = 3f + Hash(i, 2, 230) * 6f;
                var col = Hash(i, 3, 230) < 0.7f ? new Color(1f, 0.62f, 0.28f) : new Color(0.85f, 0.15f, 0.1f);
                Disc(px, W, H, cx, cy, r, col, 0.55f);
            }

            for (int i = 0; i < px.Length; i++) f.Set(i % W, i / W, px[i], 0f, 0f);
            return f;
        }

        static Field Street()
        {
            const int W = 512, H = 512;
            var f = new Field(W, H) { Bump = 0f };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)H;
                    Color c;
                    if (v < 0.35f)
                    {
                        float wet = Fbm(x, y, W, 12, 301);
                        c = new Color(0.04f, 0.04f, 0.045f) * (0.7f + wet * 0.6f);
                        // The lamp's reflection drawn down the wet tarmac.
                        float lampX = Mathf.Abs(x - W * 0.7f) / (W * 0.05f);
                        c += new Color(0.45f, 0.30f, 0.12f) * Mathf.Clamp01(1f - lampX) * (1f - v / 0.35f) * (0.4f + wet * 0.5f);
                    }
                    else c = Color.Lerp(new Color(0.07f, 0.055f, 0.05f), new Color(0.015f, 0.02f, 0.035f), (v - 0.35f) / 0.65f);
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            Blocks(px, W, H, 307, 0.55f, 0.85f, new Color(0.03f, 0.03f, 0.035f), 0.12f, 1f);
            for (int y = (int)(H * 0.3f); y < (int)(H * 0.72f); y++)
                for (int x = (int)(W * 0.7f) - 1; x <= (int)(W * 0.7f) + 1; x++)
                    px[y * W + x] = new Color(0.07f, 0.07f, 0.075f);
            Blur(px, W, H, 1);
            Disc(px, W, H, W * 0.7f, H * 0.73f, 6f, new Color(1f, 0.80f, 0.50f), 1f);
            Disc(px, W, H, W * 0.7f, H * 0.73f, 20f, new Color(0.5f, 0.35f, 0.18f), 0.12f);
            for (int i = 0; i < px.Length; i++) f.Set(i % W, i / W, px[i], 0f, 0f);
            return f;
        }

        static void Blocks(Color[] px, int W, int H, int seed, float minTop, float maxTop, Color body,
                           float litChance, float haze)
        {
            int x = 0, b = 0;
            while (x < W)
            {
                int width = (26 + (int)(Hash(b, 0, seed) * 60f)) * W / 512;
                int top = (int)(H * Mathf.Lerp(minTop, maxTop, Hash(b, 1, seed)));
                for (int y = 0; y < top; y++)
                    for (int i = x; i < Mathf.Min(W, x + width); i++)
                    {
                        int lx = i - x;
                        Color c = Mul(body, 0.85f + 0.3f * Hash(lx / 3, y / 3, seed + 2));
                        int cw = 6 * W / 512, ch = 8 * W / 512;
                        int wx = lx % cw, wy = y % ch;
                        if (wx >= cw / 3 && wx <= cw * 2 / 3 + 1 && wy >= ch * 3 / 8 && wy <= ch * 5 / 8 + 1 &&
                            y < top - 3 && lx > 1 && lx < width - 2)
                        {
                            float roll = Hash(lx / cw + b * 31, y / ch, seed + 5);
                            if (roll < litChance)
                            {
                                float k = 0.5f + 0.5f * Hash(lx / cw, y / ch + b, seed + 7);
                                c = Hash(lx / cw, y / ch + b, seed + 9) > 0.3f
                                    ? new Color(0.95f, 0.70f, 0.40f) * k : new Color(0.60f, 0.72f, 0.92f) * k;
                            }
                            else c = Mul(body, 1.5f);
                        }
                        px[y * W + i] = Color.Lerp(px[y * W + i], c, haze);
                    }
                if (b % 4 == 2 && top + 2 < H)
                    Disc(px, W, H, x + width * 0.5f, top + 1f, 1.6f, new Color(1f, 0.12f, 0.08f), 1f);
                x += width + 1 + (int)(Hash(b, 2, seed) * 8f) * W / 512;
                b++;
            }
        }

        static void Disc(Color[] px, int W, int H, float cx, float cy, float r, Color col, float strength)
        {
            int x0 = Mathf.Max(0, (int)(cx - r - 1)), x1 = Mathf.Min(W - 1, (int)(cx + r + 1));
            int y0 = Mathf.Max(0, (int)(cy - r - 1)), y1 = Mathf.Min(H - 1, (int)(cy + r + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d > 1f) continue;
                    float k = Mathf.SmoothStep(1f, 0.75f, d) * strength;
                    var c = px[y * W + x] + col * k;
                    c.a = 1f;
                    px[y * W + x] = c;
                }
        }

        static void Blur(Color[] px, int W, int H, int radius)
        {
            var tmp = new Color[px.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                var src = pass == 0 ? px : tmp;
                var dst = pass == 0 ? tmp : px;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        Color sum = Color.clear;
                        int n = 0;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int sx = pass == 0 ? Mathf.Clamp(x + k, 0, W - 1) : x;
                            int sy = pass == 0 ? y : Mathf.Clamp(y + k, 0, H - 1);
                            sum += src[sy * W + sx];
                            n++;
                        }
                        dst[y * W + x] = sum / n;
                    }
            }
        }
    }
}
