/*
 * Lone EFT DMA Radar - Copyright (c) 2026 Lone DMA
 * Licensed under GNU AGPLv3. See https://www.gnu.org/licenses/agpl-3.0.html
 */
using MemoryPack;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace LoneEftDmaRadar.Web.WebRadar.MemoryPack
{
    public sealed class MemoryPackHub : IHubProtocol
    {
        static MemoryPackHub()
        {
            MemoryPackFormatterProvider.Register(new Vector2Formatter());
            MemoryPackFormatterProvider.Register(new Vector3Formatter());
        }

        private const int MessageTypeSize = sizeof(int);
        private const int LengthPrefixSize = sizeof(int);
        private const int HeaderSize = LengthPrefixSize + MessageTypeSize;

        private enum HubMessageType
        {
            Invocation = 1,
            StreamItem = 2,
            Completion = 3,
            StreamInvocation = 4,
            CancelInvocation = 5,
            Ping = 6,
            Close = 7
        }

        public string Name => "memorypack";

        public int Version => 1;

        public TransferFormat TransferFormat => TransferFormat.Binary;

        public bool IsVersionSupported(int version) => version == Version;

        public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
        {
            var writer = new ArrayBufferWriter<byte>();
            WriteMessage(message, writer);
            return writer.WrittenMemory;
        }

        public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
        {
            switch (message)
            {
                case InvocationMessage invocation:
                    WriteFrame(output, HubMessageType.Invocation, writer =>
                    {
                        WriteString(writer, invocation.InvocationId);
                        WriteString(writer, invocation.Target);
                        WriteArguments(writer, invocation.Arguments);
                    });
                    break;

                case StreamItemMessage streamItem:
                    WriteFrame(output, HubMessageType.StreamItem, writer =>
                    {
                        WriteString(writer, streamItem.InvocationId);
                        WriteObject(writer, streamItem.Item);
                    });
                    break;

                case CompletionMessage completion:
                    WriteFrame(output, HubMessageType.Completion, writer =>
                    {
                        WriteString(writer, completion.InvocationId);
                        WriteString(writer, completion.Error);
                        WriteBool(writer, completion.HasResult);
                        if (completion.HasResult)
                            WriteObject(writer, completion.Result);
                    });
                    break;

                case StreamInvocationMessage streamInvocation:
                    WriteFrame(output, HubMessageType.StreamInvocation, writer =>
                    {
                        WriteString(writer, streamInvocation.InvocationId);
                        WriteString(writer, streamInvocation.Target);
                        WriteArguments(writer, streamInvocation.Arguments);
                    });
                    break;

                case CancelInvocationMessage cancelInvocation:
                    WriteFrame(output, HubMessageType.CancelInvocation, writer =>
                    {
                        WriteString(writer, cancelInvocation.InvocationId);
                    });
                    break;

                case PingMessage:
                    WriteFrame(output, HubMessageType.Ping, _ => { });
                    break;

                case CloseMessage close:
                    WriteFrame(output, HubMessageType.Close, writer =>
                    {
                        WriteString(writer, close.Error);
                        WriteBool(writer, close.AllowReconnect);
                    });
                    break;

                default:
                    throw new HubException($"Unexpected message type: {message.GetType().Name}");
            }
        }

        public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage? message)
        {
            message = null;

            if (input.Length < HeaderSize)
                return false;

            var lengthSlice = input.Slice(0, LengthPrefixSize);
            int frameLength = ReadInt32(ref lengthSlice);

            int totalFrameSize = LengthPrefixSize + frameLength;
            if (input.Length < totalFrameSize)
                return false;

            var frame = input.Slice(LengthPrefixSize, frameLength);
            var msgTypeSlice = frame.Slice(0, MessageTypeSize);
            var msgType = (HubMessageType)ReadInt32(ref msgTypeSlice);
            var payload = frame.Slice(MessageTypeSize).ToArray().AsSpan();

            int offset = 0;
            message = msgType switch
            {
                HubMessageType.Invocation => ParseInvocation(payload, ref offset, binder),
                HubMessageType.StreamItem => ParseStreamItem(payload, ref offset, binder),
                HubMessageType.Completion => ParseCompletion(payload, ref offset, binder),
                HubMessageType.StreamInvocation => ParseStreamInvocation(payload, ref offset, binder),
                HubMessageType.CancelInvocation => ParseCancelInvocation(payload, ref offset),
                HubMessageType.Ping => PingMessage.Instance,
                HubMessageType.Close => ParseClose(payload, ref offset),
                _ => throw new HubException($"Unknown message type: {(int)msgType}")
            };

            input = input.Slice(totalFrameSize);
            return true;
        }

        #region Parse Helpers

        private static InvocationMessage ParseInvocation(ReadOnlySpan<byte> payload, ref int offset, IInvocationBinder binder)
        {
            var invocationId = ReadString(payload, ref offset);
            var target = ReadString(payload, ref offset)!;
            var paramTypes = binder.GetParameterTypes(target);
            var args = ReadArguments(payload, ref offset, paramTypes);
            return new InvocationMessage(invocationId, target, args);
        }

        private static StreamItemMessage ParseStreamItem(ReadOnlySpan<byte> payload, ref int offset, IInvocationBinder binder)
        {
            var invocationId = ReadString(payload, ref offset)!;
            var itemType = binder.GetReturnType(invocationId);
            var item = ReadObject(payload, ref offset, itemType);
            return new StreamItemMessage(invocationId, item);
        }

        private static CompletionMessage ParseCompletion(ReadOnlySpan<byte> payload, ref int offset, IInvocationBinder binder)
        {
            var invocationId = ReadString(payload, ref offset)!;
            var error = ReadString(payload, ref offset);
            var hasResult = ReadBool(payload, ref offset);
            if (error is not null)
                return CompletionMessage.WithError(invocationId, error);
            if (hasResult)
            {
                var resultType = binder.GetReturnType(invocationId);
                var result = ReadObject(payload, ref offset, resultType);
                return CompletionMessage.WithResult(invocationId, result);
            }
            return CompletionMessage.Empty(invocationId);
        }

        private static StreamInvocationMessage ParseStreamInvocation(ReadOnlySpan<byte> payload, ref int offset, IInvocationBinder binder)
        {
            var invocationId = ReadString(payload, ref offset)!;
            var target = ReadString(payload, ref offset)!;
            var paramTypes = binder.GetParameterTypes(target);
            var args = ReadArguments(payload, ref offset, paramTypes);
            return new StreamInvocationMessage(invocationId, target, args);
        }

        private static CancelInvocationMessage ParseCancelInvocation(ReadOnlySpan<byte> payload, ref int offset)
        {
            var invocationId = ReadString(payload, ref offset)!;
            return new CancelInvocationMessage(invocationId);
        }

        private static CloseMessage ParseClose(ReadOnlySpan<byte> payload, ref int offset)
        {
            var error = ReadString(payload, ref offset);
            var allowReconnect = ReadBool(payload, ref offset);
            return new CloseMessage(error, allowReconnect);
        }

        #endregion

        #region Write Primitives

        private static void WriteFrame(IBufferWriter<byte> output, HubMessageType type, Action<IBufferWriter<byte>> writePayload)
        {
            var payloadBuffer = new ArrayBufferWriter<byte>();

            // Write message type
            var typeSpan = payloadBuffer.GetSpan(MessageTypeSize);
            BinaryPrimitives.WriteInt32LittleEndian(typeSpan, (int)type);
            payloadBuffer.Advance(MessageTypeSize);

            // Write payload
            writePayload(payloadBuffer);

            // Write length prefix + frame to output
            var lengthSpan = output.GetSpan(LengthPrefixSize);
            BinaryPrimitives.WriteInt32LittleEndian(lengthSpan, payloadBuffer.WrittenCount);
            output.Advance(LengthPrefixSize);

            output.Write(payloadBuffer.WrittenSpan);
        }

        private static void WriteString(IBufferWriter<byte> writer, string? value)
        {
            if (value is null)
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32LittleEndian(span, -1);
                writer.Advance(sizeof(int));
                return;
            }

            var encoded = System.Text.Encoding.UTF8.GetBytes(value);
            var lengthSpan = writer.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(lengthSpan, encoded.Length);
            writer.Advance(sizeof(int));
            writer.Write(encoded);
        }

        private static void WriteBool(IBufferWriter<byte> writer, bool value)
        {
            var span = writer.GetSpan(1);
            span[0] = value ? (byte)1 : (byte)0;
            writer.Advance(1);
        }

        private static void WriteObject(IBufferWriter<byte> writer, object? value)
        {
            var bytes = MemoryPackSerializer.Serialize(value?.GetType() ?? typeof(object), value);
            var lengthSpan = writer.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(lengthSpan, bytes.Length);
            writer.Advance(sizeof(int));
            writer.Write(bytes);
        }

        private static void WriteArguments(IBufferWriter<byte> writer, object?[]? args)
        {
            int count = args?.Length ?? 0;
            var countSpan = writer.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(countSpan, count);
            writer.Advance(sizeof(int));

            for (int i = 0; i < count; i++)
                WriteObject(writer, args![i]);
        }

        #endregion

        #region Read Primitives

        private static int ReadInt32(ref ReadOnlySequence<byte> sequence)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            sequence.Slice(0, sizeof(int)).CopyTo(buffer);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        private static string ReadString(ReadOnlySpan<byte> payload, ref int offset)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
            offset += sizeof(int);
            if (length == -1)
                return null;
            var value = System.Text.Encoding.UTF8.GetString(payload.Slice(offset, length));
            offset += length;
            return value;
        }

        private static bool ReadBool(ReadOnlySpan<byte> payload, ref int offset)
        {
            bool value = payload[offset] != 0;
            offset += 1;
            return value;
        }

        private static object ReadObject(ReadOnlySpan<byte> payload, ref int offset, Type type)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
            offset += sizeof(int);
            var objectBytes = payload.Slice(offset, length);
            offset += length;
            return MemoryPackSerializer.Deserialize(type, objectBytes);
        }

        private static object[] ReadArguments(ReadOnlySpan<byte> payload, ref int offset, IReadOnlyList<Type> paramTypes)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
            offset += sizeof(int);
            var args = new object[count];
            for (int i = 0; i < count; i++)
                args[i] = ReadObject(payload, ref offset, paramTypes[i]);
            return args;
        }

        #endregion
    }
}
