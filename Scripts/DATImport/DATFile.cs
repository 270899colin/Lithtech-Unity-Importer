using ExtensionMethods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class DATFile
{
    // Header
    public uint version = 0;
    public uint object_data_pos = 0;
    public uint render_data_pos = 0;

    // Lithtech Jupiter
    public uint blind_object_data_pos = 0;
    public uint light_grid_pos = 0;
    public uint collision_data_pos = 0;
    public uint particle_blocker_data_pos = 0;

    // World3D Info
    public WorldInfo world_info = null;
    // World3D Tree
    public WorldTree world_tree = null;
    // World Models
    public uint world_model_count = 0;
    public List<WorldBSP> world_models = new();
    // Render Data
    public WorldLightmaps lightmap_data = null;
    public WorldObjectHeader world_object_data = null;
    // Jupiter Only
    public RenderData render_data = null;

    // Scratch
    private int current_poly_index = 0;
    private int current_world_model_index = 0;

    private BinaryReader reader;

    public DATFile(string filePath, bool dont_import_world_models = false)
    {
        reader = new BinaryReader(new FileStream(filePath, FileMode.Open));

        this.version = reader.ReadUInt32();

        if (!IsSupported())
        {
            throw new Exception(string.Format("Unsupported file version {0}", this.version));
        }

        this.object_data_pos = reader.ReadUInt32();

        if (IsLithtechJupiter())
        {
            this.blind_object_data_pos = reader.ReadUInt32();
            this.light_grid_pos = reader.ReadUInt32();
            this.collision_data_pos = reader.ReadUInt32();
            this.particle_blocker_data_pos = reader.ReadUInt32();
        }

        this.render_data_pos = reader.ReadUInt32();

        if (!IsLithtech1())
        {
            // Skip dummy ints
            this.reader.BaseStream.Seek(8 * 4, SeekOrigin.Current);
        }

        this.world_info = new WorldInfo(reader, this);

        Debug.Log("Props: " + this.world_info.properties);

        if (!IsLithtech1())
        {
            this.world_tree = new WorldTree(reader);
        }
        else
        {
            this.world_tree = new WorldTree();
        }

        var world_model_pos = reader.BaseStream.Position;

        if (!IsLithtech1() && !IsLithtechJupiter() && this.render_data_pos != reader.BaseStream.Length)
        {
            reader.BaseStream.Seek(this.render_data_pos, SeekOrigin.Begin);

            this.lightmap_data = new WorldLightmaps(reader, this);
        }

        if (IsLithtechJupiter())
        {
            reader.BaseStream.Seek(this.render_data_pos, SeekOrigin.Begin);

            // TODO: Add Jupiter support
            //this.render_data = new RenderData();
        }

        reader.BaseStream.Seek(this.object_data_pos, SeekOrigin.Begin);

        world_object_data = new WorldObjectHeader(reader);

        if(!IsLithtech1())
        {
			reader.BaseStream.Seek(world_model_pos, SeekOrigin.Begin);
        } else
        {
			WorldModelBatchRead(1);
			this.world_models[0].world_name = "Root";
        }

		this.world_model_count = reader.ReadUInt32();
		Debug.Log("World Model Count: " + this.world_model_count);

		if(!dont_import_world_models)
        {
			WorldModelBatchRead(this.world_model_count);
        }
    }

    public void Close()
    {
        reader.Close();
    }

    public List<WorldBSP> WorldModelBatchRead(uint amount_to_read)
    {
        List<WorldBSP> world_models = new();
        for (int i = 0; i < amount_to_read; i++)
        {
            var next_world_model_pos = reader.ReadUInt32();

            if (!IsLithtechJupiter())
            {
                var unk_dummy = reader.ReadBytes(32);
            }

            var world_bsp = new WorldBSP(reader, this);

            if (world_bsp.section_count > 0)
            {
                reader.BaseStream.Seek(next_world_model_pos, SeekOrigin.Begin);
            }

            world_models.Add(world_bsp);
            this.world_models.Add(world_bsp);
        }

        return world_models;
    }

    // Version checks

    private static uint DAT_VERSION_LT1 = 56;
    private static uint DAT_VERSION_LT15 = 57;
    private static uint DAT_VERSION_PSYCHO = 127;
    private static uint DAT_VERSION_NOLF = 66;
    private static uint DAT_VERSION_AVP2 = 70;
    private static uint DAT_VERSION_JUPITER = 85;

    private static List<uint> SUPPORTED_VERSIONS = new() { DAT_VERSION_LT1, DAT_VERSION_LT15, DAT_VERSION_PSYCHO, DAT_VERSION_NOLF, DAT_VERSION_AVP2 };

    public bool IsLithtech1(bool include15 = true, bool include_psycho = true)
    {
        List<uint> to_check = new() { DAT_VERSION_LT1 };
        if (include15)
        {
            to_check.Add(DAT_VERSION_LT15);
        }
        if (include_psycho)
        {
            to_check.Add(DAT_VERSION_PSYCHO);
        }
        return to_check.Contains(this.version);
    }

    public bool IsLithtech15(bool include_psycho = true)
    {
        if (!include_psycho)
        {
            return this.version == DAT_VERSION_LT15;
        }
        else
        {
            return this.version == DAT_VERSION_LT15 || this.version == DAT_VERSION_PSYCHO;
        }
    }

    public bool IsLithtechPsycho()
    {
        return this.version == DAT_VERSION_PSYCHO;
    }

    public bool IsLithtech2()
    {
        return this.version == DAT_VERSION_NOLF;
    }

    public bool IsLithtechTalon()
    {
        return this.version == DAT_VERSION_AVP2;
    }

    public bool IsLithtechJupiter()
    {
        return this.version == DAT_VERSION_JUPITER;
    }

    public bool IsSupported()
    {
        return SUPPORTED_VERSIONS.Contains(this.version);
    }

    // Internal classes

    public class WorldInfo
    {
        public string properties = "";
        public float light_map_grid_size = 0;
        public Vector3 extents_min = new();
        public Vector3 extents_max = new();
        public Vector3 world_offset = new();

        public WorldInfo(BinaryReader reader, DATFile dat)
        {
            this.properties = reader.ReadDATString(false);

            if (dat.IsLithtech1())
            {
                if (dat.IsLithtech15())
                {
                    this.light_map_grid_size = reader.ReadSingle();
                }

                // Skip dummy ints
                reader.BaseStream.Seek(8 * 4, SeekOrigin.Current);
            }
            else
            {
                if (!dat.IsLithtechJupiter())
                {
                    this.light_map_grid_size = reader.ReadSingle();
                }

                this.extents_min = reader.ReadVector3();
                this.extents_max = reader.ReadVector3();

                if (dat.IsLithtechJupiter())
                {
                    this.world_offset = reader.ReadVector3();
                }
            }
        }
    }

    public class WorldTree
    {
        public WorldTreeNode root_node = null;
        public WorldTree(BinaryReader reader)
        {
            var node = new WorldTreeNode(reader);
            root_node = node;
        }
        public WorldTree() { }
    }

    public class WorldTreeNode
    {
        public const int max_world_tree_children = 4;

        public Vector3 box_min = new Vector3();
        public Vector3 box_max = new Vector3();
        public uint child_node_count = 0;
        public uint dummy_terrain_depth = 0;

        public double center_x = 0;
        public double center_y = 0;
        public float smallest_dim = 0;

        public List<WorldTreeNode> child_nodes = new();

        public void SetBoundingBox(Vector3 min_vec, Vector3 max_vec)
        {
            this.box_min = min_vec;
            this.box_max = max_vec;

            this.center_x = (max_vec.x + min_vec.x) * 0.5;
            this.center_y = (max_vec.z + min_vec.z) * 0.5;

            this.smallest_dim = Math.Min(max_vec.x - min_vec.x, max_vec.z - min_vec.z);
        }

        public Tuple<byte, int, int> ReadLayout(BinaryReader reader, byte current_byte, int current_bit, int current_offset)
        {
            if (current_bit == 8)
            {
                current_byte = reader.ReadByte();
                current_bit = 0;
            }

            bool subdivide = (current_byte & (1 << current_bit)) != 0;

            if (subdivide)
            {
                Subdivide(current_offset);

                foreach (WorldTreeNode node in child_nodes)
                {
                    var ret = node.ReadLayout(reader, current_byte, current_bit, current_offset);
                    current_byte = ret.Item1;
                    current_bit = ret.Item2;
                    current_offset = ret.Item3;
                }
            }

            return Tuple.Create(current_byte, current_bit, current_offset);
        }

        public void Subdivide(int current_offset)
        {
            for (int i = 0; i < max_world_tree_children; i++)
            {
                var node = new WorldTreeNode();
                node.SetBoundingBox(this.box_min, this.box_max);
                this.child_nodes.Add(node);
            }
        }
        public WorldTreeNode() { }

        public WorldTreeNode(BinaryReader reader)
        {
            // For root node
            this.box_min = reader.ReadVector3();
            this.box_max = reader.ReadVector3();
            this.child_node_count = reader.ReadUInt32();
            this.dummy_terrain_depth = reader.ReadUInt32();

            this.SetBoundingBox(this.box_min, this.box_max);

            this.ReadLayout(reader, 0, 8, 0);
        }
    }

    public class WorldTexture
    {
        public string name;

        public WorldTexture(BinaryReader reader)
        {
            // Null terminated string

            List<byte> byte_array = new();
            byte current_byte = reader.ReadByte();

            while (current_byte != 0x0)
            {
                byte_array.Add(current_byte);
                current_byte = reader.ReadByte();
            }

            this.name = Encoding.ASCII.GetString(byte_array.ToArray());
        }
    }

    public class WorldPlane
    {
        public Vector3 normal = new();
        public float distance = 0.0f;

        public WorldPlane(BinaryReader reader)
        {
            this.normal = reader.ReadVector3();
            this.distance = reader.ReadSingle();
        }
    }

    public class WorldLeaf
    {
        public class LeafData
        {
            public short portal_id = 0;
            public ushort size = 0;
            public byte[] contents;

            public LeafData(BinaryReader reader)
            {
                this.portal_id = reader.ReadInt16();
                this.size = reader.ReadUInt16();
                this.contents = reader.ReadBytes(size);
            }
        }

        public uint count = 0;
        public int index = -1;
        public List<LeafData> data = new();
        public int polygon_count = 0;
        public byte[] polygon_data = { };
        public float unk_1 = 0.0f;

        public WorldLeaf(BinaryReader reader, DATFile dat)
        {
            this.count = reader.ReadUInt16();

            if (this.count == 0xFFFF)
            {
                this.index = reader.ReadUInt16();
            }
            else
            {
                for (int i = 0; i < this.count; i++)
                {
                    LeafData leaf_data = new LeafData(reader);
                    data.Add(leaf_data);
                }
            }

            // No extra data for Jupiter
            if (!dat.IsLithtechJupiter())
            {
                if (dat.IsLithtech1())
                {
                    this.polygon_count = reader.ReadUInt16();
                }
                else
                {
                    this.polygon_count = (int)reader.ReadUInt32();
                }

                this.polygon_data = reader.ReadBytes(this.polygon_count * 4);

                if (dat.IsLithtech1())
                {
                    this.unk_1 = reader.ReadSingle();
                }
                else
                {
                    this.unk_1 = reader.ReadUInt32();
                }
            }
        }
    }

    public class WorldSurface
    {
        public Vector3 uv1 = new();
        public Vector3 uv2 = new();
        public Vector3 uv3 = new();

        // Lithtech 1.0
        public Vector3 uv4 = new();
        public Vector3 uv5 = new();
        public Color32 color = new();

        public int unknown = 0;
        public int unknown2 = 0;

        public ushort texture_flags = 0;
        public ushort texture_index = 0;
        public int flags = 0;
        public byte use_effects = 0;
        public string effect_name = "";
        public string effect_param = "";

        public void FixColor(Vector3 colorData)
        {
            Color32 color = new Color32();

            color.r = (byte)Math.Min(255.0, colorData.x);
            color.g = (byte)Math.Min(255.0, colorData.y);
            color.b = (byte)Math.Min(255.0, colorData.z);

            this.color = color;
        }

        public WorldSurface(BinaryReader reader, DATFile dat)
        {
            if (dat.IsLithtechJupiter())
            {
                this.flags = (int)reader.ReadUInt32();
                this.texture_index = reader.ReadUInt16();
                this.texture_flags = reader.ReadUInt16();
            }
            else
            {
                this.uv1 = reader.ReadVector3();
                this.uv2 = reader.ReadVector3();
                this.uv3 = reader.ReadVector3();

                if (dat.IsLithtech1())
                {
                    this.uv4 = reader.ReadVector3();
                    this.uv5 = reader.ReadVector3();
                    Vector3 colorData = reader.ReadVector3();
                    // # Color data can be filled with 0xCDCDCDCD
                    FixColor(colorData);
                }

                this.texture_index = reader.ReadUInt16();

                if (dat.IsLithtech1() || dat.IsLithtech2())
                {
                    this.unknown = reader.ReadInt32();
                }

                this.flags = reader.ReadInt32();

                this.unknown2 = reader.ReadInt32();

                this.use_effects = reader.ReadByte();

                if (this.use_effects == 1)
                {
                    this.effect_name = reader.ReadDATString();
                    this.effect_param = reader.ReadDATString();
                }

                this.texture_flags = reader.ReadUInt16();

                if (dat.IsLithtechPsycho())
                {
                    var unknown_short = reader.ReadUInt16();
                }
            }
        }
    }

    public class WorldPoly
    {
        public class DiskVert
        {
            public int vertex_index = 0;
            public byte[] dummy = { };
            public Color32 color = new Color32(255, 255, 255, 255);

            public void FixColor()
            {
                this.color.r = (byte)Math.Min(255.0, dummy[0]);
                this.color.g = (byte)Math.Min(255.0, dummy[1]);
                this.color.b = (byte)Math.Min(255.0, dummy[2]);
            }

            public DiskVert(BinaryReader reader, DATFile dat)
            {
                if (dat.IsLithtechJupiter())
                {
                    this.vertex_index = reader.ReadInt32();
                }
                else
                {
                    this.vertex_index = reader.ReadInt16();
                    this.dummy = reader.ReadBytes(3);

                    if (dat.IsLithtech1())
                    {
                        FixColor();
                    }
                }
            }
        }

        public Vector3 center = new();
        public short lightmap_width = 0;
        public short lightmap_height = 0;

        public short unknown_flag = 0;
        public List<int> unknown_list = new();

        public int surface_index = 0;
        public int plane_index = 0;

        public Vector3 uv1 = new();
        public Vector3 uv2 = new();
        public Vector3 uv3 = new();

        public List<DiskVert> disk_verts = new();

        public Texture2D lightmap_texture = null;

        public WorldPoly(BinaryReader reader, DATFile dat, int vert_count = 0)
        {
            if (dat.IsLithtechJupiter())
            {
                this.surface_index = reader.ReadInt32();
                this.plane_index = reader.ReadInt32();
                for (int i = 0; i < vert_count; i++)
                {
                    DiskVert diskVert = new DiskVert(reader, dat);
                    disk_verts.Add(diskVert);
                }
            }
            else
            {
                if (!dat.IsLithtech1())
                {
                    this.center = reader.ReadVector3();
                }

                this.lightmap_width = reader.ReadInt16();
                this.lightmap_height = reader.ReadInt16();

                if (!dat.IsLithtech1())
                {
                    this.unknown_flag = reader.ReadInt16();
                    for (int i = 0; i < this.unknown_flag * 2; i++)
                    {
                        this.unknown_list.Add(reader.ReadInt16());
                    }
                }

                if (dat.IsLithtech1())
                {
                    var unknown_1 = reader.ReadInt32();
                    var unknown_2 = reader.ReadInt32();
                    this.surface_index = reader.ReadInt32();

                }
                else if (dat.IsLithtech2())
                {
                    this.surface_index = reader.ReadInt16();
                    this.plane_index = reader.ReadInt16();

                }
                else if (dat.IsLithtechTalon())
                {
                    this.surface_index = reader.ReadInt32();
                    this.plane_index = reader.ReadInt32();

                    this.uv1 = reader.ReadVector3();
                    this.uv2 = reader.ReadVector3();
                    this.uv3 = reader.ReadVector3();
                }

                for (int i = 0; i < vert_count; i++)
                {
                    var diskVert = new DiskVert(reader, dat);
                    disk_verts.Add(diskVert);
                }

                // Process lightmaps
                if (dat.IsLithtech2())
                {
                    int world_model_index = dat.current_world_model_index;

                    if (!dat.lightmap_data.data[0].sorted_data.ContainsKey(world_model_index))
                    {
                        dat.current_poly_index += 1;
                        return;
                    }

                    var lightmap_data_list = dat.lightmap_data.data[0].sorted_data[world_model_index];
                    Tuple<int, int[]> lightmap_data = null;

                    if (this.lightmap_width + this.lightmap_height == 0)
                    {
                        dat.current_poly_index += 1;
                        return;
                    }

                    foreach (var data in lightmap_data_list)
                    {
                        if (data.Item1 == dat.current_poly_index)
                        {
                            lightmap_data = data;
                        }
                    }

                    if (lightmap_data == null)
                    {
                        dat.current_poly_index += 1;
                        return;
                    }

                    var lm_width = this.lightmap_width;
                    var lm_height = this.lightmap_height;
                    var color_data = lightmap_data.Item2;

                    // TODO: Apply lightmap to texture
                    // lightmap_texture = new Texture2D(lm_width, lm_height, TextureFormat.RGBA32, false);
                    //lightmap_texture.LoadRawTextureData(color_data);
                    //texture.LoadRawTextureData(color_data.ToArray());
                    //Texture.Apply
                }

                dat.current_poly_index += 1;
            }
        }
    }

    public class WorldNode
    {
        const int NFI_NODE_UNDEFINED = -1;
        const int NFI_NODE_IN = 0;
        const int NFI_NODE_OUT = 1;
        const int NFI_ERROR = 3;
        const int NFI_OK = 4;

        public int index = 0;
        public int index_2 = 0;

        public int poly_index = 0;
        public short leaf_index = 0;

        public int[] status = { NFI_NODE_UNDEFINED, NFI_NODE_UNDEFINED };

        public int GetNodeStatus(int index, int node_count)
        {
            if (index == -1)
            {
                return NFI_NODE_IN;
            }
            else if (index == -1) // Copied from Godot plugin, probably incorrect but don't know what it should be.
            {
                return NFI_NODE_OUT;
            }
            else if (index >= node_count)
            {
                return NFI_ERROR;
            }

            return NFI_OK;
        }

        public WorldNode(BinaryReader reader, DATFile dat, int node_count = 0)
        {
            if (dat.IsLithtech1())
            {
                int unknown_intro = reader.ReadInt32();
            }

            this.poly_index = reader.ReadInt32();
            // TODO: polygons > WorldBSP.Poly_Count
            this.leaf_index = reader.ReadInt16();

            this.index = reader.ReadInt32();
            this.index_2 = reader.ReadInt32();

            this.status[0] = this.GetNodeStatus(this.index, node_count);
            this.status[1] = this.GetNodeStatus(this.index_2, node_count);

            if (dat.IsLithtech1())
            {
                Quaternion unknown = reader.ReadQuaternion();
            }
        }

        public WorldNode() { }
    }

    public class WorldUserPortal
    {
        public string name = "";
        public int unk_int_1 = 0;
        public int unk_int_2 = 0;
        public ushort unk_short = 0;

        public Vector3 center = new();
        public Vector3 dims = new();

        public WorldUserPortal(BinaryReader reader, DATFile dat)
        {
            this.name = reader.ReadDATString();
            this.unk_int_1 = reader.ReadInt32();

            if (!dat.IsLithtech1())
            {
                this.unk_int_2 = reader.ReadInt32();
            }

            this.unk_short = reader.ReadUInt16();

            this.center = reader.ReadVector3();
            this.dims = reader.ReadVector3();
        }
    }

    public class WorldPBlockRecord
    {
        public short size = 0;
        public short unk_short = 0;
        public byte[] contents = { };

        public WorldPBlockRecord(BinaryReader reader)
        {
            this.size = reader.ReadInt16();
            this.unk_short = reader.ReadInt16();

            contents = reader.ReadBytes(6 * this.size);
        }
    }

    public class WorldPBlockTable
    {
        public int unk_int_1 = 0;
        public int unk_int_2 = 0;
        public int unk_int_3 = 0;

        public int size = 0;

        public Vector3 unk_vector_1 = new();
        public Vector3 unk_vector_2 = new();

        public List<WorldPBlockRecord> records = new();

        public WorldPBlockTable(BinaryReader reader)
        {
            this.unk_int_1 = reader.ReadInt32();
            this.unk_int_2 = reader.ReadInt32();
            this.unk_int_3 = reader.ReadInt32();

            this.size = this.unk_int_1 * this.unk_int_2 * this.unk_int_3;

            this.unk_vector_1 = reader.ReadVector3();
            this.unk_vector_2 = reader.ReadVector3();

            for (int i = 0; i < this.size; i++)
            {
                WorldPBlockRecord record = new WorldPBlockRecord(reader);
                this.records.Add(record);
            }
        }
    }

    public class WorldBSP
    {
        public int world_info_flags = 0;
        public string world_name = "";

        public int point_count = 0;
        public int plane_count = 0;
        public int surface_count = 0;
        public int user_portal_count = 0;
        public int poly_count = 0;
        public int leaf_count = 0;
        public int vert_count = 0;
        public int total_vis_list_size = 0;
        public int leaf_list_count = 0;
        public int node_count = 0;
        public int section_count = 0;

        public Vector3 min_box = new();
        public Vector3 max_box = new();
        public Vector3 world_translation = new();
        public int name_length = 0;
        public int texture_count = 0;

        public List<string> texture_names = new();
        public List<byte> verts = new();
        public List<Vector3> points = new();
        public List<WorldPoly> polies = new();
        public List<WorldPlane> planes = new();
        public List<WorldSurface> surfaces = new();
        public List<WorldLeaf> leafs = new();
        public List<WorldNode> nodes = new();
        public List<WorldUserPortal> user_portals = new();
        public WorldPBlockTable block_table = null;
        public WorldNode root_node = null;

        public WorldBSP(BinaryReader reader, DATFile dat)
        {
            this.world_info_flags = reader.ReadInt32();

            if (!dat.IsLithtech1() && !dat.IsLithtechJupiter())
            {
                var unknown_value = reader.ReadInt32();
            }

            this.world_name = reader.ReadDATString();

            if (dat.IsLithtech1())
            {
                var next_position = reader.ReadInt32();
            }

            this.point_count = reader.ReadInt32();
            this.plane_count = reader.ReadInt32();
            this.surface_count = reader.ReadInt32();
            this.user_portal_count = reader.ReadInt32();
            this.poly_count = reader.ReadInt32();
            this.leaf_count = reader.ReadInt32();
            this.vert_count = reader.ReadInt32();
            this.total_vis_list_size = reader.ReadInt32();
            this.leaf_list_count = reader.ReadInt32();
            this.node_count = reader.ReadInt32();

            if (!dat.IsLithtechJupiter())
            {
                var unknown_value_2 = reader.ReadInt32();
            }

            if (!dat.IsLithtech1() && !dat.IsLithtechJupiter())
            {
                var unknown_value_3 = reader.ReadInt32();
            }

            this.min_box = reader.ReadVector3();
            this.max_box = reader.ReadVector3();
            this.world_translation = reader.ReadVector3();

            this.name_length = reader.ReadInt32();
            this.texture_count = reader.ReadInt32();

            for (int i = 0; i < this.texture_count; i++)
            {
                var texture = new WorldTexture(reader);
                texture_names.Add(texture.name);
            }

            for (int i = 0; i < this.poly_count; i++)
            {
                var vert = reader.ReadByte();
                if (!dat.IsLithtechJupiter())
                {
                    vert += reader.ReadByte();
                }
                this.verts.Add(vert);
            }

            for (int i = 0; i < this.leaf_count; i++)
            {
                var leaf = new WorldLeaf(reader, dat);
                this.leafs.Add(leaf);
            }

            for (int i = 0; i < this.plane_count; i++)
            {
                var plane = new WorldPlane(reader);
                this.planes.Add(plane);
            }

            for (int i = 0; i < this.surface_count; i++)
            {
                var surface = new WorldSurface(reader, dat);
                this.surfaces.Add(surface);
            }

            if (dat.IsLithtechTalon())
            {
                for (int i = 0; i < this.point_count; i++)
                {
                    this.points.Add(reader.ReadVector3());
                }
            }

            int biggest_lm_width = 0;
            int biggest_lm_height = 0;

            for (int i = 0; i < this.poly_count; i++)
            {
                var poly = new WorldPoly(reader, dat, this.verts[i]);

                if (poly.lightmap_width > biggest_lm_width)
                {
                    biggest_lm_width = poly.lightmap_width;
                }

                if (poly.lightmap_height > biggest_lm_height)
                {
                    biggest_lm_height = poly.lightmap_height;
                }

                this.polies.Add(poly);
            }

            for (int i = 0; i < this.node_count; i++)
            {
                var node = new WorldNode(reader, dat, this.node_count);
                this.nodes.Add(node);
            }

            for (int i = 0; i < this.user_portal_count; i++)
            {
                var portal = new WorldUserPortal(reader, dat);
                this.user_portals.Add(portal);
            }

            if (!dat.IsLithtechTalon())
            {
                for (int i = 0; i < this.point_count; i++)
                {
                    this.points.Add(reader.ReadVector3());

                    if (dat.IsLithtech2())
                    {
                        var normal = reader.ReadVector3();
                    }
                }
            }

            if (!dat.IsLithtechJupiter())
            {
                this.block_table = new WorldPBlockTable(reader);
            }

            this.root_node = new WorldNode();
            this.root_node.index = reader.ReadInt32();
            this.root_node.status = new int[] { this.root_node.GetNodeStatus(this.root_node.index, this.node_count) };

            dat.current_world_model_index += 1;
            dat.current_poly_index = 0;

            if (!dat.IsLithtech1())
            {
                this.section_count = reader.ReadInt32();
                if (this.section_count > 0)
                {
                    Debug.Log("WorldModel has terrain sections > 0!");
                }
            }
            else
            {
                var unknown_count = reader.ReadInt32();

                List<Vector3> polygon_list = new();
                for (int i = 0; i < this.poly_count; i++)
                {
                    polygon_list.Add(reader.ReadVector3());
                }

                var lightmap_count = reader.ReadInt32();

                if (lightmap_count > 0)
                {
                    foreach (var poly in polies)
                    {
                        var surface = this.surfaces[poly.surface_index];
                        var chk = surface.flags & (1 << 7);
                        // TODO: Test
                        if (chk == 0)
                        {
                            continue;
                        }

                        var lm_width = reader.ReadByte();
                        var lm_height = reader.ReadByte();
                        List<int> color_data = new();

                        for (int i = 0; i < (lm_width * lm_height); i++)
                        {
                            var packed_color = reader.ReadInt16();

                            color_data.Add((packed_color & 0xF800) >> 8);
                            color_data.Add((packed_color & 0x07E0) >> 3);
                            color_data.Add((packed_color & 0x001F) << 3);
                        }

                        // TODO: Apply lightmap to texture
                    }
                }
            }
        }
    }

    public class WorldLightmapFrame
    {
        public short world_model_index = 0;
        public short poly_index = 0;

        public WorldLightmapFrame(BinaryReader reader)
        {
            this.world_model_index = reader.ReadInt16();
            this.poly_index = reader.ReadInt16();
        }
    }

    public class WorldLightmapBatch
    {
        public int size = 0;
        public int[] data = { };

        private void DecompressData(BinaryReader reader)
        {
            int current_position = 0;
            int safety_break = 1024;
            List<int> color_data = new();

            while (current_position < this.size)
            {
                byte copy_count = 0;
                int tag = reader.ReadInt16();
                current_position += 2;

                var chk = tag & 0x8000;
                if (chk != 0)
                {
                    copy_count = reader.ReadByte();
                    tag = tag & 0x7FFF;
                    current_position += 1;
                }
                else
                {
                    copy_count = 1;
                }

                safety_break -= copy_count;
                if (safety_break < 0)
                {
                    throw new Exception("LM Data over-read detected");
                }

                for (int i = 0; i < copy_count; i++)
                {
                    int r = (tag >> 10) & 0x001F;
                    int g = (tag >> 5) & 0x001F;
                    int b = (tag) & 0x001F;

                    color_data.Add(r);
                    color_data.Add(g);
                    color_data.Add(b);
                }
            }

            this.data = color_data.ToArray();
        }

        public WorldLightmapBatch(BinaryReader reader, DATFile dat, int type)
        {
            if (dat.IsLithtech2())
            {
                this.size = reader.ReadInt32();
            }
            else
            {
                this.size = reader.ReadInt16();
            }

            if (type > 0)
            {
                var data = Array.ConvertAll(reader.ReadBytes(this.size), Convert.ToInt32);
            }
            else
            {
                DecompressData(reader);
            }
        }
    }

    public class WorldLightmapColor
    {
        public int vertex_count = 0;
        public byte[] r = { };
        public byte[] g = { };
        public byte[] b = { };

        public WorldLightmapColor(BinaryReader reader)
        {
            this.vertex_count = reader.ReadByte();
            this.r = reader.ReadBytes(this.vertex_count);
            this.g = reader.ReadBytes(this.vertex_count);
            this.b = reader.ReadBytes(this.vertex_count);
        }
    }

    public class WorldLightmapData
    {
        public string name = "";
        public int type = 0;
        public int batch_count = 0;
        public int frame_count = 0;

        public List<WorldLightmapFrame> frames = new();
        public List<WorldLightmapBatch> batches = new();
        public List<WorldLightmapColor> colors = new();

        // Key: World model index
        // Value: List containing tuples of 1: Poly index, 2: WorldLightmapBatch data
        public Dictionary<int, List<Tuple<int, int[]>>> sorted_data = new();

        public WorldLightmapData(BinaryReader reader, DATFile dat)
        {
            this.name = reader.ReadDATString();
            this.type = reader.ReadInt32();

            if (dat.IsLithtech2())
            {
                this.batch_count = reader.ReadInt32();
                this.frame_count = reader.ReadInt32();
            }
            else
            {
                this.batch_count = reader.ReadByte();
                this.frame_count = reader.ReadInt16();
            }

            // TODO: Use class instead of tuple
            for (int i = 0; i < this.frame_count; i++)
            {
                var frame = new WorldLightmapFrame(reader);
                frames.Add(new WorldLightmapFrame(reader));

                if (sorted_data.ContainsKey(frame.world_model_index))
                {
                    this.sorted_data[frame.world_model_index].Add(new Tuple<int, int[]>(frame.poly_index, null));
                }
                else
                {
                    var tuple = new Tuple<int, int[]>(frame.poly_index, null);
                    this.sorted_data.Add(frame.world_model_index, new List<Tuple<int, int[]>>() { tuple });
                }
            }

            int frame_index = 0;

            for (int i = 0; i < this.frame_count; i++)
            {
                var batch = new WorldLightmapBatch(reader, dat, type);

                batches.Add(batch);

                int model_index = this.frames[frame_index].world_model_index;

                for (int j = 0; j < this.sorted_data[model_index].Count; j++)
                {
                    if (this.frames[frame_index].poly_index == this.sorted_data[model_index][j].Item1)
                    {
                        var tuple = new Tuple<int, int[]>(this.sorted_data[model_index][j].Item1, batch.data);
                        this.sorted_data[model_index][j] = tuple;
                    }
                }

                frame_index += 1;
            }

            if (dat.IsLithtech2())
            {

            }
            else
            {
                for (int i = 0; i < this.frame_count; i++)
                {
                    var color = new WorldLightmapColor(reader);
                    this.colors.Add(color);
                }
            }
        }
    }

    public class WorldLightmaps
    {
        public int total_frames_1 = 0;
        public int total_animations = 0;
        public int total_memory = 0;
        public int total_frames_2 = 0;
        public int count = 0;

        public List<WorldLightmapData> data = new();

        public WorldLightmaps(BinaryReader reader, DATFile dat)
        {
            this.total_frames_1 = reader.ReadInt32();
            this.total_animations = reader.ReadInt32();
            this.total_memory = reader.ReadInt32();
            this.total_frames_2 = reader.ReadInt32();
            this.count = reader.ReadInt32();

            for (int i = 0; i < this.count; i++)
            {
                var item = new WorldLightmapData(reader, dat);
                data.Add(item);
            }
        }
    }

    public class ObjectProperty
    {
        public const int PROP_STRING = 0;
        public const int PROP_VECTOR = 1;
        public const int PROP_COLOR = 2;
        public const int PROP_FLOAT = 3;
        public const int PROP_FLAGS = 4;
        public const int PROP_BOOL = 5;
        public const int PROP_LONG_INT = 6;
        public const int PROP_ROTATION = 7;

        public const int PROP_UNK_INT = 9; // KPC

        public string name = "";
        public int code = 0;
        public short data_length = 0;
        public int flags = 0;

        public object value = null;

        public ObjectProperty(BinaryReader reader)
        {
            this.name = reader.ReadDATString();
            this.code = reader.ReadByte();
            this.flags = reader.ReadInt32();
            this.data_length = reader.ReadInt16();

            if (this.code > 7 && this.code != 9)
            {
                Debug.Log(String.Format("Unknown code in ObjectProperty '{0}'", this.code));
            }

            switch (this.code)
            {
                case PROP_STRING:
                    this.value = reader.ReadDATString();
                    break;
                case PROP_VECTOR:
                    this.value = reader.ReadVector3();
                    break;
                case PROP_COLOR:
                    this.value = reader.ReadVector3();
                    break;
                case PROP_FLOAT:
                    this.value = reader.ReadSingle();
                    break;
                case PROP_BOOL:
                    this.value = reader.ReadByte();
                    break;
                case PROP_FLAGS:
                    this.value = reader.ReadInt32();
                    break;
                case PROP_LONG_INT:
                    this.value = reader.ReadInt32();
                    break;
                case PROP_UNK_INT:
                    this.value = reader.ReadInt32();
                    break;
                case PROP_ROTATION:
                    this.value = reader.ReadQuaternion();
                    break;
            }
        }
    }

    public class WorldObject
    {
        public ushort data_length = 0;
        public string name = "";
        public int property_count = 0;
        public List<ObjectProperty> properties = new();

        public WorldObject(BinaryReader reader)
        {
            this.data_length = reader.ReadUInt16();
            this.name = reader.ReadDATString(true);
            this.property_count = reader.ReadInt32();

            for (int i = 0; i < this.property_count; i++)
            {
                var property = new ObjectProperty(reader);
                this.properties.Add(property);
            }
        }
    }

    public class WorldObjectHeader
    {
        public int count = 0;
        public List<WorldObject> world_objects = new();

        public WorldObjectHeader(BinaryReader reader)
        {
            this.count = reader.ReadInt32();

            for (int i = 0; i < this.count; i++)
            {
                var world_object = new WorldObject(reader);
                world_objects.Add(world_object);
            }
        }
    }

    public class RenderData
    {
        // Jupiter only
        // TODO
    }
}
