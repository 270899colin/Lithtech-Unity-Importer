using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ExtensionMethods
{
    public static class BinaryReaderExtensions
    {
		public static string ReadDATString(this BinaryReader reader, bool is_length_a_short = true)
		{
			var length = 0;
			if (is_length_a_short)
			{
				length = reader.ReadInt16();
			}
			else
			{
				length = reader.ReadInt32();
			}

			return Encoding.ASCII.GetString(reader.ReadBytes(length));
		}

		public static Vector2 ReadVector2(this BinaryReader reader)
		{
			var vector = new Vector2();
			vector.x = reader.ReadSingle();
			vector.y = reader.ReadSingle();
			return vector;
		}

		public static Vector3 ReadVector3(this BinaryReader reader)
		{
			var vector = new Vector3();
			vector.x = reader.ReadSingle();
			vector.y = reader.ReadSingle();
			vector.z = reader.ReadSingle();
			return vector;
		}

		public static Quaternion ReadQuaternion(this BinaryReader reader)
		{
			Quaternion quat = new Quaternion();
			quat.w = reader.ReadSingle();
			quat.x = reader.ReadSingle();
			quat.y = reader.ReadSingle();
			quat.z = reader.ReadSingle();
			return quat;
		}
	}

}