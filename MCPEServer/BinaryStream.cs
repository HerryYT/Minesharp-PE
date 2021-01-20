using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace MCPEServer
{
    class BinaryStream
    {
        private readonly MemoryStream stream;
        private readonly BinaryReader reader;
        private readonly BinaryWriter writer;

        public BinaryStream()
        {
            stream = new MemoryStream();

            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        public BinaryStream(byte[] bytes)
        {
            stream = new MemoryStream(bytes);

            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        public byte[] ReadBytes(int len)
        {
            return reader.ReadBytes(len);
        }

        public void WriteBytes(byte[] v)
        {
            writer.Write(v);
        }

        public byte ReadByte()
        {
            return reader.ReadByte();
        }

        public void WriteByte(byte v)
        {
            writer.Write(v);
        }

        public bool ReadBoolean()
        {
            return ReadByte() != 0x00;
        }

        public void WriteBoolean(bool v)
        {
            writer.Write(v);
        }

        public long ReadLong()
        {
            return reader.ReadInt64();
        }

        public void WriteLong(long v)
        {
            writer.Write(v);
        }

        public short ReadShort(bool bigEndian = false)
        {
            if (bigEndian)
            {
                return BinaryPrimitives.ReverseEndianness(reader.ReadInt16());
            }
            return reader.ReadInt16();
        }

        public void WriteShort(short v)
        {
            writer.Write(v);
        }

        public ushort ReadUShort()
        {
            return reader.ReadUInt16();
        }

        public void WriteUShort(ushort v)
        {
            writer.Write(v);
        }

        public void WriteString(string str)
        {
            WriteUShort((ushort)str.Length);
            WriteBytes(new ASCIIEncoding().GetBytes(str));
        }
        
        // Int24, 3 bytes integer
        public int ReadTriad()
        {
            byte[] buffer = ReadBytes(3);
            return buffer[0] + (buffer[1] << 8) + (buffer[2] << 16);
        }

        public void WriteTriad(int v)
        {
            byte[] buffer = new byte[3];
            buffer[0] = (byte)v;
            buffer[1] = (byte)(v >> 8);
            buffer[2] = (byte)(v >> 16);
            WriteBytes(buffer);
        }

        public long Lenght()
        {
            return stream.Length;
        }

        public byte[] ToByteArray()
        {
            /* 
            Console.WriteLine(string.Format("Position={0}, Length={1}", stream.Position, stream.Length));
            if (stream.Position != stream.Length)
            {
                int unreadSize = (int)stream.Length - (int)stream.Position;
                byte[] unreadBytes = new byte[1024];
                Array.Copy(stream.ToArray(), (int)stream.Position, unreadBytes, 0, unreadSize);
                Console.WriteLine("Still {0} unread bytes: {1}", unreadSize, BitConverter.ToString(unreadBytes));
            }
            */
            return stream.ToArray();
        }
    }
}
