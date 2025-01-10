using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CokeProtocol
{
    // Protocol Constants
    public static class CokeProtocolConstants
    {
        public const ushort DEFAULT_PORT = 7654;
        public const byte PROTOCOL_VERSION = 0x01;
        public const ushort MAX_PACKET_SIZE = 1024;
        public const byte MAGIC_BYTE = 0xCC;  // CC for CoCa-Cola
        
        // Packet Types
        public const byte INIT_REQUEST = 0x01;
        public const byte INIT_RESPONSE = 0x02;
        public const byte RECIPE_PROPOSAL = 0x03;
        public const byte RECIPE_VALIDATION = 0x04;
        public const byte CONSENSUS_REQUEST = 0x05;
        public const byte CONSENSUS_RESPONSE = 0x06;
        public const byte VERIFICATION_REQUEST = 0x07;
        public const byte VERIFICATION_RESPONSE = 0x08;
        public const byte FINALIZATION = 0x09;
        public const byte ERROR = 0xFF;

        // Error Codes
        public const byte ERR_INVALID_VERSION = 0x01;
        public const byte ERR_INVALID_RECIPE = 0x02;
        public const byte ERR_CONSENSUS_FAILED = 0x03;
        public const byte ERR_VERIFICATION_FAILED = 0x04;
    }

    // Packet Header Structure
    public class CokePacketHeader
    {
        public byte Magic { get; set; }         // Magic byte (0xCC)
        public byte Version { get; set; }        // Protocol version
        public byte Type { get; set; }           // Packet type
        public ushort Length { get; set; }       // Payload length
        public byte Flags { get; set; }          // Control flags
        public byte Checksum { get; set; }       // Header checksum

        public byte[] Serialize()
        {
            byte[] header = new byte[7];
            header[0] = Magic;
            header[1] = Version;
            header[2] = Type;
            header[3] = (byte)(Length >> 8);
            header[4] = (byte)(Length & 0xFF);
            header[5] = Flags;
            header[6] = CalculateChecksum(header);
            return header;
        }

        private byte CalculateChecksum(byte[] data)
        {
            byte sum = 0;
            for (int i = 0; i < data.Length - 1; i++)
                sum ^= data[i];
            return sum;
        }
    }

    // Recipe Component Structure
    public class RecipeComponent
    {
        public string Name { get; set; }
        public double Ratio { get; set; }
        public byte[] Hash { get; set; }
    }

    // Protocol Node Implementation
    public class CokeProtocolNode : IDisposable
    {
        private readonly Socket _socket;
        private readonly ConcurrentDictionary<string, NodeState> _peerStates;
        private readonly byte[] _receiveBuffer;
        private bool _isLeader;
        private readonly string _nodeId;

        public enum NodeState
        {
            Disconnected,
            Initializing,
            Active,
            Validating,
            Consensus,
            Error
        }

        public CokeProtocolNode(string nodeId, bool isLeader = false)
        {
            _nodeId = nodeId;
            _isLeader = isLeader;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _peerStates = new ConcurrentDictionary<string, NodeState>();
            _receiveBuffer = new byte[CokeProtocolConstants.MAX_PACKET_SIZE];
        }

        public void StartListening(int port = CokeProtocolConstants.DEFAULT_PORT)
        {
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
            _socket.Listen(10);
            BeginAccept();
        }

        private void BeginAccept()
        {
            try
            {
                _socket.BeginAccept(AcceptCallback, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Accept error: {ex.Message}");
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = _socket.EndAccept(ar);
                HandleNewConnection(client);
                BeginAccept();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Accept callback error: {ex.Message}");
            }
        }

        private void HandleNewConnection(Socket client)
        {
            try
            {
                // Start receiving data from the new client
                StateObject state = new StateObject
                {
                    WorkSocket = client,
                    Buffer = new byte[CokeProtocolConstants.MAX_PACKET_SIZE]
                };

                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                    ReceiveCallback, state);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Handle connection error: {ex.Message}");
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket client = state.WorkSocket;

            try
            {
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    ProcessPacket(state.Buffer, bytesRead, client);
                    
                    // Continue receiving
                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                        ReceiveCallback, state);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Receive error: {ex.Message}");
                client.Close();
            }
        }

        private void ProcessPacket(byte[] data, int length, Socket client)
        {
            if (length < 7) // Minimum header size
                return;

            var header = ParseHeader(data);
            if (!ValidateHeader(header))
            {
                SendError(client, CokeProtocolConstants.ERR_INVALID_VERSION);
                return;
            }

            switch (header.Type)
            {
                case CokeProtocolConstants.INIT_REQUEST:
                    HandleInitRequest(data, header, client);
                    break;

                case CokeProtocolConstants.RECIPE_PROPOSAL:
                    HandleRecipeProposal(data, header, client);
                    break;

                case CokeProtocolConstants.CONSENSUS_REQUEST:
                    HandleConsensusRequest(data, header, client);
                    break;

                // ... handle other packet types
            }
        }

        private CokePacketHeader ParseHeader(byte[] data)
        {
            return new CokePacketHeader
            {
                Magic = data[0],
                Version = data[1],
                Type = data[2],
                Length = (ushort)((data[3] << 8) | data[4]),
                Flags = data[5],
                Checksum = data[6]
            };
        }

        private bool ValidateHeader(CokePacketHeader header)
        {
            if (header.Magic != CokeProtocolConstants.MAGIC_BYTE)
                return false;

            if (header.Version != CokeProtocolConstants.PROTOCOL_VERSION)
                return false;

            byte calculatedChecksum = 0;
            // ... calculate checksum
            
            return calculatedChecksum == header.Checksum;
        }

        private void HandleInitRequest(byte[] data, CokePacketHeader header, Socket client)
        {
            // Process initialization request
            var response = CreatePacket(CokeProtocolConstants.INIT_RESPONSE, new
            {
                NodeId = _nodeId,
                IsLeader = _isLeader,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            SendPacket(client, response);
        }

        private void HandleRecipeProposal(byte[] data, CokePacketHeader header, Socket client)
        {
            if (!_isLeader)
            {
                SendError(client, CokeProtocolConstants.ERR_INVALID_RECIPE);
                return;
            }

            // ... handle recipe proposal
        }

        private byte[] CreatePacket(byte type, object payload)
        {
            var jsonPayload = JsonSerializer.SerializeToUtf8Bytes(payload);
            var header = new CokePacketHeader
            {
                Magic = CokeProtocolConstants.MAGIC_BYTE,
                Version = CokeProtocolConstants.PROTOCOL_VERSION,
                Type = type,
                Length = (ushort)jsonPayload.Length,
                Flags = 0
            };

            var headerBytes = header.Serialize();
            var packet = new byte[headerBytes.Length + jsonPayload.Length];
            Buffer.BlockCopy(headerBytes, 0, packet, 0, headerBytes.Length);
            Buffer.BlockCopy(jsonPayload, 0, packet, headerBytes.Length, jsonPayload.Length);

            return packet;
        }

        private void SendPacket(Socket client, byte[] packet)
        {
            try
            {
                client.BeginSend(packet, 0, packet.Length, 0, SendCallback, client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send error: {ex.Message}");
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                int bytesSent = client.EndSend(ar);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send callback error: {ex.Message}");
            }
        }

        private void SendError(Socket client, byte errorCode)
        {
            var packet = CreatePacket(CokeProtocolConstants.ERROR, new { ErrorCode = errorCode });
            SendPacket(client, packet);
        }

        public void Dispose()
        {
            _socket?.Close();
        }
    }

    // State object for receiving data
    public class StateObject
    {
        public Socket WorkSocket = null;
        public const int BufferSize = CokeProtocolConstants.MAX_PACKET_SIZE;
        public byte[] Buffer = new byte[BufferSize];
    }

    // Example usage
    public class Program
    {
        static void Main()
        {
            // Create a leader node
            using var leaderNode = new CokeProtocolNode("Leader_1", isLeader: true);
            leaderNode.StartListening(CokeProtocolConstants.DEFAULT_PORT);

            // Create some follower nodes
            for (int i = 0; i < 3; i++)
            {
                using var followerNode = new CokeProtocolNode($"Follower_{i}");
                followerNode.StartListening(CokeProtocolConstants.DEFAULT_PORT + i + 1);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
