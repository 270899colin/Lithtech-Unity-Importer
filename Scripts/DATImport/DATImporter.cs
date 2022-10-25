using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

public class DATImporter : Editor
{
    const float LIGHTMAP_ATLAS_SIZE = 2048.0f;

    // TODO: Make this configurable via settings provider
    const string gameRootDir = "D:\\Games\\Psycho\\PSYCHO\\";
    const float importScale = 0.01f;

    // TODO: Lightmaps, Jupiter support
    [MenuItem("LithTech/Import .DAT")]
    private static void Start()
    {
        string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (!assetPath.ToLower().EndsWith(".dat"))
        {
            Debug.LogError("Unsupported file format.");
            return;
        }

        //DATFile datFile = new DATFile("D:\\Games\\Psycho\\PSYCHO\\WORLDS\\DEATHMATCH\\PM01.DAT", true);
        DATFile datFile = new DATFile(assetPath, true);

        var fileName = assetPath.Split('/').Last();
        var root = new GameObject(fileName.Substring(0, fileName.Length - 4));

        uint world_model_count = datFile.world_model_count;
        uint world_model_index = 0;
        uint batch_by = 1024;

        int total_mesh_count = 0;

        // Hack for LT1
        foreach (var world_model in datFile.world_models)
        {
            var data = FillArrayMesh(datFile, new DATFile.WorldBSP[] { world_model });

            var meshes = data.Item1;
            var mesh_names = data.Item2;
            var tex_names = data.Item3;

            var use_lightmaps = false;

            // TODO: Enable lightmaps

            var i = 0;

            foreach (var mesh in meshes)
            {
                PlaceMesh(mesh, mesh_names[i], tex_names[i], root.transform);
                i++;
                total_mesh_count++;
            }
        }

        if(!datFile.IsLithtechJupiter())
        {
            while(world_model_index < world_model_count)
            {
                if((world_model_index + batch_by) > world_model_count)
                {
                    batch_by = world_model_count - world_model_index;
                }

                var world_models = datFile.WorldModelBatchRead(batch_by);
                world_model_index += batch_by;

                var data = FillArrayMesh(datFile, world_models.ToArray());
                var meshes = data.Item1;
                var mesh_names = data.Item2;
                var tex_names = data.Item3;

                var i = 0;

                foreach (var mesh in meshes)
                {
                    PlaceMesh(mesh, mesh_names[i], tex_names[i], root.transform);
                    i++;
                    total_mesh_count++;
                }
            }
        }

        if(datFile.IsLithtechPsycho())
        {
            PlaceObjects(datFile, root.transform);
        }

        root.transform.localScale *= importScale;

        datFile.Close();
    }

    // VERY experimental, only tested with KPC
    private static void PlaceObjects(DATFile datFile, Transform root)
    {
        foreach (var wo in datFile.world_object_data.world_objects)
        {
            switch (wo.name)
            {
                case "Light":
                    var props = wo.properties;
                    Vector3 pos = (Vector3)props["Pos"].value;
                    var go = new GameObject("Light");
                    go.transform.position = pos;
                    go.transform.SetParent(root);
                    Debug.Log(props["LightColor"].value);
                    Debug.Log(props["LightRadius"].value);

                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    var color = (Vector3)props["LightColor"].value;
                    light.color = new Color32((byte)color.x, (byte)color.y, (byte)color.z, 255);
                    light.range = (float)props["LightRadius"].value * importScale;

                    /*foreach (var item in wo.properties)
                    {
                        //Debug.Log(item.name);
                    }*/
                    break;
                case "DirLight":
                    break;
                default:
                    break;
            }
        }
    }

    // TODO: Make scale, shader configurable
    private static void PlaceMesh(Mesh mesh, string meshName, string texName, Transform parent)
    {
        var meshObject = new GameObject(meshName);

        meshObject.transform.SetParent(parent.transform);
        var render = meshObject.AddComponent<MeshRenderer>();

        var mat = new Material(Shader.Find("Standard"));
        //var mat = new Material(Shader.Find("PSXEffects/PS1Shader"));
        mat.SetTexture("_MainTex", GetTexture(texName));
        render.material = mat;
        var filter = meshObject.AddComponent<MeshFilter>();        
        filter.mesh = mesh;
        var collider = meshObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    private static Tuple<List<Mesh>, List<string>, List<string>> FillArrayMesh(DATFile datFile, DATFile.WorldBSP[] worldModels)
    {
        var last_lm_uv = new Vector2(0, 0);

        var textured_meshes = new Dictionary<string, List<TexturedMeshData>>();
        var lightmap_frame_index = 0;

        // TODO: big_lightmap_image

        foreach (var world_model in worldModels)
        {
            // Skip physics mesh
            if (world_model.world_name == "VisBSP" || world_model.world_name == "PhysicsBSP")
            {
                continue;
            }

            int total_lms = 0;
            int total_lm_width = 0;
            int total_lm_height = 0;
            int largest_lm_width = 0;
            int largest_lm_height = 0;
            foreach (var poly in world_model.polies)
            {
                var surface = world_model.surfaces[poly.surface_index];

                if (poly.lightmap_texture != null)
                {
                    total_lms += 1;

                    var poly_width = poly.lightmap_texture.width;
                    var poly_height = poly.lightmap_texture.height;

                    if (total_lm_width + poly_width > LIGHTMAP_ATLAS_SIZE)
                    {
                        total_lm_height += 16; // Max lm height for shogo
                        total_lm_width = 0;
                    }

                    total_lm_width += poly_width;
                    largest_lm_width = Math.Max(largest_lm_width, poly_width);
                    largest_lm_height = Math.Max(largest_lm_height, poly_height);
                }
            }

            foreach (var poly in world_model.polies)
            {
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var uvs2 = new List<Vector2>();
                var normals = new List<Vector3>();
                var colors = new List<Color32>();

                int texture_index = 0;

                var surface = world_model.surfaces[poly.surface_index];

                texture_index = surface.texture_index;

                string texture_name = world_model.texture_names[texture_index];

                var texture = GetTexture(texture_name);
                int tex_width = 256;
                int tex_height = 256;

                if (texture != null)
                {
                    tex_width = texture.width;
                    tex_height = texture.height;
                }

                DATFile.WorldPlane plane;

                if (datFile.IsLithtech1())
                {
                    plane = world_model.planes[surface.unknown];
                } else
                {
                    plane = world_model.planes[poly.plane_index];
                }

                var lm_image = poly.lightmap_texture;

                var depth_uv = new Vector2(0, 0);

                if (lm_image != null)
                {
                    if (last_lm_uv.x + lm_image.width > LIGHTMAP_ATLAS_SIZE)
                    {
                        last_lm_uv.y += 32;
                        last_lm_uv.x = 0;

                        // TODO: Big lightmap

                        depth_uv = last_lm_uv;
                        last_lm_uv.x += lm_image.width;
                    }
                }

                var v_width = 0;
                var v_height = 0;

                var last_vert = new Vector3();

                foreach (var diskVert in poly.disk_verts)
                {
                    var vert = world_model.points[diskVert.vertex_index];

                    var uv1 = new Vector3();
                    var uv2 = new Vector3();
                    var uv3 = new Vector3();

                    if (datFile.IsLithtech1() || datFile.IsLithtech2())
                    {
                        uv1 = surface.uv1;
                        uv2 = surface.uv2;
                        uv3 = surface.uv3;
                    } else
                    {
                        uv1 = poly.uv1;
                        uv2 = poly.uv2;
                        uv3 = poly.uv3;
                    }

                    verts.Add(vert);
                    normals.Add(plane.normal);

                    if (datFile.IsLithtech1())
                    {
                        colors.Add(diskVert.color);
                    }

                    var uv = OpqToUV(vert, uv1, uv2, uv3, tex_width, tex_height);
                    uvs.Add(uv);
                }

                // Start UV 2

                if (lm_image != null && lm_image.width > 0 && lm_image.height > 0)
                {
                    var lm_width = lm_image.width;
                    var lm_height = lm_image.height;

                    var poly_u = Vector3.Cross(plane.normal, Vector3.up);

                    if (Vector3.Dot(poly_u, poly_u) < 0.001)
                    {
                        poly_u = Vector3.right;
                    } else
                    {
                        poly_u = poly_u.normalized;
                    }
                    var poly_v = Vector3.Cross(plane.normal, poly_u).normalized;

                    // First pass - Find bounds

                    var top_left = new Vector2(999.0f, 999.0f);
                    var bottom_right = new Vector2(-999.0f, -999.0f);

                    var uv_offset = (new Vector2(0, 0) - top_left);
                    var uv_scale = (bottom_right - top_left);

                    foreach (var diskVert in poly.disk_verts)
                    {
                        var vert = world_model.points[diskVert.vertex_index];

                        var vert_uv = GetVertUV(vert, poly_u, poly_v, LIGHTMAP_ATLAS_SIZE, LIGHTMAP_ATLAS_SIZE);
                        var vert_offset = (depth_uv / new Vector2(LIGHTMAP_ATLAS_SIZE, LIGHTMAP_ATLAS_SIZE));

                        vert_uv += uv_offset;

                        if (uv_scale.x > 0.0f)
                        {
                            vert_uv.x /= uv_scale.x;
                        }
                        if (uv_scale.y > 0.0f)
                        {
                            vert_uv.y /= uv_scale.y;
                        }

                        var new_vert_uv = new Vector2(vert_uv.x * lm_width / LIGHTMAP_ATLAS_SIZE, vert_uv.y * lm_height / LIGHTMAP_ATLAS_SIZE);

                        new_vert_uv += vert_offset;

                        if (float.IsNaN(new_vert_uv.x))
                        {
                            new_vert_uv.x = 0;
                        }
                        if (float.IsNaN(new_vert_uv.y))
                        {
                            new_vert_uv.y = 0;
                        }

                        uvs2.Add(new_vert_uv);
                    }
                } else
                {
                    foreach (var diskVert in poly.disk_verts)
                    {
                        uvs2.Add(new Vector2(1, 0));
                    }
                }

                if (textured_meshes.ContainsKey(texture_name))
                {
                    textured_meshes[texture_name].Add(new TexturedMeshData(uvs, normals, verts, colors, uvs2));
                } else
                {
                    textured_meshes.Add(texture_name, new List<TexturedMeshData> { new TexturedMeshData(uvs, normals, verts, colors, uvs2) });
                }

                lightmap_frame_index += 1;
            }

            // big_lightmap_image.save_png("./lm_atlas.png")
        }

        return BuildArrayMesh(textured_meshes);
    }

    private static Tuple<List<Mesh>, List<string>, List<string>> BuildArrayMesh(Dictionary<string, List<TexturedMeshData>> textured_meshes)
    {
        List<Mesh> meshes = new();
        List<string> texture_references = new();
        List<string> mesh_names = new();

        foreach (var texture in textured_meshes.Keys)
        {
            var builder = new MeshBuilder();
            var batches = textured_meshes[texture];

            foreach (var meshData in batches)
            {
                var mesh_uvs = meshData.uvs.ToArray();
                var mesh_normals = meshData.normals.ToArray();
                var mesh_verts = meshData.verts.ToArray();
                var mesh_colors = meshData.colors.ToArray();
                var mesh_uvs2 = meshData.uvs2.ToArray();

                // Mesh is formatted in triangle fan segments
                builder.AddTriangleFan(mesh_verts, mesh_uvs, mesh_colors, mesh_uvs2, mesh_normals);
            }

            meshes.Add(builder.GetMesh());

            texture_references.Add(texture);
            mesh_names.Add("World Model");
        }

        return Tuple.Create(meshes, mesh_names, texture_references);
    }

    private static Vector2 OpqToUV(Vector3 vertex, Vector3 o, Vector3 p, Vector3 q, float texWidth = 128.0f, float texHeight = 128.0f)
    {
        var point = vertex - o;

        var u = Vector3.Dot(point, p) / texWidth;
        var v = Vector3.Dot(point, q) / texHeight;

        return new Vector2(u, v);
    }

    private static Vector2 GetVertUV(Vector3 vert, Vector3 poly_u, Vector3 poly_v, float lmWidth, float lmHeight)
    {
        float x = Vector3.Dot(vert, poly_u) / lmWidth;
        float y = Vector3.Dot(vert, poly_v) / lmHeight;
        return new Vector2(x, y);
    }

    // TODO: Cache textures
    private static Texture2D GetTexture(string texName)
    {
        var texPath = gameRootDir + texName;
        if(File.Exists(texPath))
        {
            var dtxFile = new DTXFile(texPath);
            return dtxFile.GetTexture();
        } else
        {
            Debug.LogWarning(String.Format("Texture '{0}' not found at path '{1}'", texName, texPath));
            return null;
        }
    }

    private class TexturedMeshData
    {
        public List<Vector3> verts = new List<Vector3>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<Vector2> uvs2 = new List<Vector2>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Color32> colors = new List<Color32>();

        public TexturedMeshData(List<Vector2> uvs, List<Vector3> normals, List<Vector3> verts, List<Color32> colors, List<Vector2> uvs2)
        {
            this.verts = verts;
            this.uvs = uvs;
            this.uvs2 = uvs2;
            this.normals = normals;
            this.colors = colors;
        }
    }

    private class MeshBuilder {
        public List<Vector3> verts = new List<Vector3>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<Vector2> uvs2 = new List<Vector2>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Color32> colors = new List<Color32>();
        public List<int> triangles = new();

        private int vertCount = 0;

        public Mesh GetMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.normals = normals.ToArray();
            mesh.colors32 = colors.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.uv2 = uvs2.ToArray();
            mesh.triangles = triangles.ToArray();
            return mesh;
        }

        public void AddTriangleFan(Vector3[] vertices, Vector2[] uvs, Color32[] colors, Vector2[] uvs2, Vector3[] normals)
        {
            for (int i = 0; i < vertices.Length - 2; i++)
            {
                AddPoint(0);
                AddPoint(i + 1);
                AddPoint(i + 2);
            }

            void AddPoint(int n)
            {
                this.verts.Add(vertices[n]);
                this.colors.Add(colors[n]);
                this.normals.Add(normals[n]);
                this.uvs.Add(uvs[n]);
                this.uvs2.Add(uvs2[n]);
                this.triangles.Add(vertCount);
                vertCount++;
            }
        }
    }
}