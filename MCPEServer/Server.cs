using System;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace MCPEServer
{
    class Server
    {
        private bool running = false;
        private readonly long serverId = 0x00000000372cdc9e;
        private readonly List<IPEndPoint> sessions = new List<IPEndPoint>();
        private readonly Dictionary<IPEndPoint, int> packetOutOrder = new Dictionary<IPEndPoint, int>();
        private readonly Dictionary<IPEndPoint, int> packetInOrder = new Dictionary<IPEndPoint, int>();

        public void ReceiveMessages(int port)
        {
            running = true;

            IPEndPoint address = new IPEndPoint(IPAddress.Any, port);
            UdpClient client = new UdpClient(address);

            Console.WriteLine(string.Format("Listening on port={0}!", port));
            
            while (running)
            {
                byte[] message = client.Receive(ref address);
                if (sessions.Contains(address))
                {
                    HandleInternal(message, client, address);
                }
                else
                {
                    HandleMessage(message, client, address);
                }
            }
        }

        private void HandleMinecraft(byte[] message, UdpClient client, IPEndPoint address)
        {
            BinaryStream dataPacket = new BinaryStream(message);
            byte id = dataPacket.ReadByte();

            switch (id)
            {
                case 0x09:
                    // Client Connect
                    long clientId = dataPacket.ReadLong();
                    long session = dataPacket.ReadLong();  // To send back in 0x10
                    dataPacket.ReadByte();  // Unknown

                    Console.WriteLine(string.Format("DataPacket 0x09: clientId={0}, session={1}", clientId, session));
                    
                    // Server Handshake
                    BinaryStream handshake = new BinaryStream();
                    handshake.WriteByte(0x10);
                    handshake.WriteBytes(new byte[] { 0x04, 0x3F, 0x57, 0xFE });  // Cookie
                    handshake.WriteByte(0xCD);  // Security flags
                    handshake.WriteShort(19132);  // Server port

                    // https://c4k3.github.io/wiki.vg/Pocket_Minecraft_Protocol.html#Server_Handshake_.280x10.29
                    byte[] unknown1 = new byte[] { 0xf5, 0xff, 0xff, 0xf5 };
                    byte[] unknown2 = new byte[] { 0xff, 0xff, 0xff, 0xff };
                    handshake.WriteTriad(unknown1.Length);
                    handshake.WriteBytes(unknown1);
                    for (int i = 0; i < 9; i++)
                    {
                        handshake.WriteTriad(unknown2.Length);
                        handshake.WriteBytes(unknown2);
                    }

                    handshake.WriteBytes(new byte[] { 0x00, 0x00 });  // Unknown
                    handshake.WriteLong(session);
                    handshake.WriteBytes(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x04, 0x44, 0x0B, 0xA9 });  // Unknown

                    SendEncapsulated(handshake.ToByteArray(), client, address);
                    break;
                default:
                    Console.WriteLine(string.Format("Unhandled DataPacket with id={0}", id));
                    break;
            }
        }

        private void SendEncapsulated(byte[] buffer, UdpClient client, IPEndPoint address)
        {
            BinaryStream encapsulated = new BinaryStream();
            packetOutOrder.TryGetValue(address, out int num);
            encapsulated.WriteTriad(num);  // Encapsulated count
            packetOutOrder[address] = num + 1;  

            encapsulated.WriteByte(0x00);  // Encapsulated Id, always 0x00 when sending
            encapsulated.WriteShort((short)(buffer.Length * 8));  // Datapacket length in 
            encapsulated.WriteBytes(buffer);  // Write datapacket content

            client.Send(encapsulated.ToByteArray(), (int)encapsulated.Lenght(), address);

            Console.WriteLine(string.Format("Sent encapsulated with count={0}", num));
        }

        // Handles encapsulateds
        private void HandleInternal(byte[] message, UdpClient client, IPEndPoint address)
        {
            BinaryStream encapsulated = new BinaryStream(message);
            byte id = encapsulated.ReadByte();  // Skip id, we just need the flag honestly

            if (id == 0xA0)
            {
                // NAK
                Console.WriteLine("Got a NAK");
            } 
            else if (id == 0xC0)
            {
                // ACK
                Console.WriteLine("Got an ACK");
            } 
            else
            {
                // Packet order index
                int count = encapsulated.ReadTriad();

                // Let know MCPE we received the packet
                BinaryStream ack = new BinaryStream();
                ack.WriteByte(0xC0);
                packetOutOrder.TryGetValue(address, out int num);
                ack.WriteShort((short)num);
                packetOutOrder[address] = num + 1;
                ack.WriteBoolean(true); // Contains only one number
                ack.WriteTriad(count);  // Count from 0x80

                SendEncapsulated(ack.ToByteArray(), client, address);

                byte encapsulationId = encapsulated.ReadByte();

                short length = encapsulated.ReadShort(true);  // Packet length in bits (both headers excluded)
                int byteLength = length / 8;  // Packet length in bytes

                switch (encapsulationId)
                {
                    case 0x00:
                        // When i need to send a packet, encapsulated header always is 0x00
                        break;
                    case 0x40:
                    case 0x60:
                        // Unknown but probably is realible packet ordering id
                        byte[] weirdCountBytes = encapsulated.ReadBytes(3);
                        int weirdCount = weirdCountBytes[0] + (weirdCountBytes[1] << 8) + (weirdCountBytes[2] << 16);
                        Console.WriteLine(string.Format("Unknown count id={0}, non reliable count id={1}", weirdCount, count));

                        if (encapsulationId == 0x60)
                        {
                            // Here we have some additional fields
                            encapsulated.ReadBytes(4); // Unknown, sent just on first iteration
                        }
                        break;
                    default:
                        Console.WriteLine(string.Format("Unhandled Encapsulated with id={0}, order={1}", encapsulationId, count));
                        break;
                }

                byte[] dataPacketSlice = encapsulated.ReadBytes(byteLength);
                // TODO: check if it contains other packets
                HandleMinecraft(dataPacketSlice, client, address);
            }
        }

        private void HandleMessage(byte[] message, UdpClient client, IPEndPoint address)
        {
            BinaryStream packet = new BinaryStream(message);
            byte id = packet.ReadByte();

            // Console.WriteLine(string.Format("Recived packet with id={0}", id.ToString("X2")));

            switch (id)
            {
                case 0x01:
                    {
                        // Unconnected Ping
                        long timestamp = packet.ReadLong(); // Time since start in ms
                        byte[] magic = packet.ReadBytes(16);  // Magic bytes
                                                              // long clientGUID = packet.ReadLong();
                        Console.WriteLine(string.Format("Got UnconnectedPing timestamp={0}", timestamp));

                        // Reply with Unconnected Pong
                        BinaryStream pong = new BinaryStream();
                        pong.WriteByte(0x1c);
                        pong.WriteLong(timestamp);
                        pong.WriteLong(serverId); // Server Id
                        pong.WriteBytes(magic); // Magic bytes
                        pong.WriteString("MCPE;Steve;2 7;0.11.0;0;20");

                        Console.WriteLine(string.Format("Sent UnconnectedPong timestamp={0} serverId={1}, name=MCPE;Steve;2 7;0.11.0;0;20", timestamp, serverId));

                        client.Send(pong.ToByteArray(), (int)pong.Lenght(), address);
                    }
                    break;
                case 0x05:
                    {
                        // Open Connection Request 1
                        byte[] magic = packet.ReadBytes(16);  // Magic bytes
                        byte protocolVersion = packet.ReadByte();
                        // Skip MTU (1447 * 0x00) bytes

                        Console.WriteLine(string.Format("Got OpenConnectionRequest1 protocolVersion={0}", protocolVersion));

                        BinaryStream reply = new BinaryStream();
                        reply.WriteByte(0x06);
                        reply.WriteBytes(magic); // Magic bytes
                        reply.WriteLong(serverId); // Server Id
                        reply.WriteByte(0x00); // Server security, always 0
                        reply.WriteShort(1447); // MTU (Maximum Transfer Unit) size

                        Console.WriteLine(string.Format("Sent OpenConnectionReply1 serverId={0}, security=false, mtuSize=1447", serverId));

                        client.Send(reply.ToByteArray(), (int)reply.Lenght(), address);
                    }
                    break;
                case 0x07:
                    {
                        // Open Connection Request 2
                        byte[] magic = packet.ReadBytes(16); // Magic bytes
                        byte addressFamily = packet.ReadByte(); // Address family

                        Debug.Assert(addressFamily == 4, "Address family V6 is not supported yet");

                        byte[] addr = packet.ReadBytes(4); // Server address
                        ushort port = packet.ReadUShort(); // Server port
                        short mtu = packet.ReadShort();  // MTU
                        long clientId = packet.ReadLong(); // Client Id same for a given device

                        string strAddr = Encoding.UTF8.GetString(addr);
                        Console.WriteLine(string.Format("Got OpenConnectionRequest2 address={0}, port={1}, mtu={2}, clientId={3}", strAddr, port, mtu, clientId));

                        BinaryStream reply = new BinaryStream();
                        reply.WriteByte(0x08);
                        reply.WriteBytes(magic); // Magic bytes
                        reply.WriteLong(serverId); // Server Id
                        reply.WriteShort((short)address.Port); // Client port
                        reply.WriteShort(1447); // MTU
                        reply.WriteByte(0x00); // Security

                        Console.WriteLine(string.Format("Sent OpenConnectionReply2 serverId={0}, clientPort={1}, mtuSize=1447, security=false", serverId, address.Port));

                        // From now we will receive RakNet packets
                        sessions.Add(address);
                        packetOutOrder.Add(address, 0);
                        packetInOrder.Add(address, 0);

                        client.Send(reply.ToByteArray(), (int)reply.Lenght(), address);
                    }
                    break;
                default:
                    Console.WriteLine(string.Format("Unhandled packet with id={0}", id.ToString("X2")));
                    break;
            }
        }
    }
}
