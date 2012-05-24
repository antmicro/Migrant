using System;
using System.IO;

namespace AntMicro.AntSerializer
{
    public class FileToSerializableWrapper : ISpeciallySerializable
    {
        public void Load(PrimitiveReader reader)
        {
            UnderlyingPath = Path.GetTempFileName();
            using(var file = new FileStream(UnderlyingPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var size = reader.ReadInt64();
                reader.CopyTo(file, size);
            }
        }

        public void Save(PrimitiveWriter writer)
        {
            using(var file = new FileStream(UnderlyingPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                writer.Write(file.Length);
                writer.CopyFrom(file, file.Length);
            }
        }

        public string UnderlyingPath { get; set; }
    }
}

