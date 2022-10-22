using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class DTXFile
{
    private BinaryReader reader;

    // Versioning
    private const int DTX_VERSION_LT1 = -2;
    private const int DTX_VERSION_LT15 = -3;
    private const int DTX_VERSION_LT2 = -5;

    // Resource Types
    private const int RESOURCE_TYPE_DTX = 0;
    private const int RESOURCE_TYPE_MODEL = 1;
    private const int RESOURCE_TYPE_SPRITE = 2;

    // Flags
    private const int DTX_FULLBRITE = (1 << 0);
    private const int DTX_PREFER16BIT = (1 << 1);
    private const int DTX_MIPSALLOCED = (1 << 2);
    private const int DTX_SECTIONSFIXED = (1 << 3);
    private const int DTX_NOSYSCACHE = (1 << 6);
    private const int DTX_PREFER4444 = (1 << 7);
    private const int DTX_PREFER5551 = (1 << 8);
    private const int DTX_32BITSYSCOPY = (1 << 9);
    private const int DTX_CUBEMAP = (1 << 10);
    private const int DTX_BUMPMAP = (1 << 11);
    private const int DTX_LUMBUMPMAP = (1 << 12);
    private const int DTX_FLAGSAVEMASK = (DTX_FULLBRITE | DTX_32BITSYSCOPY | DTX_PREFER16BIT | DTX_SECTIONSFIXED | DTX_PREFER4444 | DTX_PREFER5551 | DTX_CUBEMAP | DTX_BUMPMAP | DTX_LUMBUMPMAP | DTX_NOSYSCACHE);

    private const int DTX_COMMANDSTRING_LENGTH = 128;

    private const int BPP_8P = 0;
    private const int BPP_8 = 1;
    private const int BPP_16 = 2;
    private const int BPP_32 = 3;
    private const int BPP_S3TC_DXT1 = 4;
    private const int BPP_S3TC_DXT3 = 5;
    private const int BPP_S3TC_DXT5 = 6;
    private const int BPP_32P = 7;

    // Header
    private int resource_type = 0;
    private int version = 0;
    private int width = 0;
    private int height = 0;
    private int mipmap_count = 0;
    private int section_count = 0;
    private int flags = 0;
    private int user_flags = 0;

    // Extra data
    private int texture_group = 0;
    private int mipmaps_to_use = 0;
    private int bytes_per_pixel = 0;
    private int mipmap_offset = 0;
    private int mipmap_tex_coord_offset = 0;
    private int texture_priority = 0;
    private float detail_texture_scale = 0.0f;
    private int detail_texture_angle = 0;
    private string command_string = "";

    private Texture2D texture;

    public DTXFile(string filePath)
    {
        reader = new BinaryReader(new FileStream(filePath, FileMode.Open));

        this.resource_type = reader.ReadInt32();

        if (this.resource_type != 0)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
        }

        this.version = reader.ReadInt32();

        if (this.version != DTX_VERSION_LT1 && this.version != DTX_VERSION_LT15 && this.version != DTX_VERSION_LT2)
        {
            throw new System.Exception(string.Format("Unsupported file version {0}", this.version));
        }

        this.width = reader.ReadInt16();
        this.height = reader.ReadInt16();
        this.mipmap_count = reader.ReadInt16();
        this.section_count = reader.ReadInt16();
        this.flags = reader.ReadInt32();
        this.user_flags = reader.ReadInt32();

        this.texture_group = reader.ReadByte();
        this.mipmaps_to_use = reader.ReadByte();
        this.bytes_per_pixel = reader.ReadByte();
        this.mipmap_offset = reader.ReadByte();
        this.mipmap_tex_coord_offset = reader.ReadByte();
        this.texture_priority = reader.ReadByte();
        this.detail_texture_scale = reader.ReadSingle();
        this.detail_texture_angle = reader.ReadInt16();

        if (this.version == DTX_VERSION_LT15 || this.version == DTX_VERSION_LT2)
        {
            // TODO: Investigate (seems broken)

            this.command_string = Encoding.ASCII.GetString(reader.ReadBytes(DTX_COMMANDSTRING_LENGTH));
            //this.command_string = new string(reader.ReadChars(DTX_COMMANDSTRING_LENGTH));
            //Debug.Log(this.command_string);
        }

        ReadTextureData();

        reader.Close();
    }

    private void ReadTextureData()
    {
        if (this.version == DTX_VERSION_LT15 || this.version == DTX_VERSION_LT2 || this.bytes_per_pixel == BPP_8P)
        {
            Read8BitPalette();
        } else if (this.bytes_per_pixel == BPP_S3TC_DXT1 || this.bytes_per_pixel == BPP_S3TC_DXT3 || this.bytes_per_pixel == BPP_S3TC_DXT5)
        {
            ReadCompressed();
        } else if (this.bytes_per_pixel == BPP_32)
        {
            Read32BitTexture();
        } else if (this.bytes_per_pixel == BPP_32P)
        {
            Read32BitPalette();
        }
    }

    private void ReadCompressed()
    {
        // TODO
    }

    private void Read8BitPalette()
    {
        // Internal use
        int _palette_header_1 = reader.ReadInt32();
        int _palette_header_2 = reader.ReadInt32();

        List<Color32> palette = new List<Color32>();
        List<byte> color_data = new List<byte>();

        int i = 0;
        for (i = 0; i < 256; i++)
        {
            byte a = reader.ReadByte();
            byte r = reader.ReadByte();
            byte g = reader.ReadByte();
            byte b = reader.ReadByte();

            palette.Add(new Color32(r, g, b, a));
        }

        var data = reader.ReadBytes(this.width * this.height * 1);

        int j = 0;
        while (j < data.Length)
        {
            color_data.Add(palette[data[j]].r);
            color_data.Add(palette[data[j]].g);
            color_data.Add(palette[data[j]].b);
            color_data.Add(palette[data[j]].a);
            j++;
        }

        texture = new Texture2D(this.width, this.height, TextureFormat.RGBA32, false);
        texture.LoadRawTextureData(color_data.ToArray());

        // TODO: Find out why texture is flipped
        FlipTexture(ref texture);

        texture.Apply(false);
    }

    private void Read32BitTexture()
    {
        // TODO
    }

    private void Read32BitPalette()
    {
        // TODO
    }

    public Texture2D GetTexture()
    {
        return this.texture;
    }

    private static void FlipTexture(ref Texture2D texture)
    {
        int textureWidth = texture.width;
        int textureHeight = texture.height;

        Color32[] pixels = texture.GetPixels32();

        // Flip horizontally
        for (int y = 0; y < textureHeight; y++)
        {
            int yo = y * textureWidth;
            for (int il = yo, ir = yo + textureWidth - 1; il < ir; il++, ir--)
            {
                Color32 col = pixels[il];
                pixels[il] = pixels[ir];
                pixels[ir] = col;
            }
        }

        // Flip vertically
        System.Array.Reverse(pixels, 0, pixels.Length);
        texture.SetPixels32(pixels);
        texture.Apply();
    }
}
