/*
 * Lone EFT DMA Radar - Copyright (c) 2026 Lone DMA
 * Licensed under GNU AGPLv3. See https://www.gnu.org/licenses/agpl-3.0.html
 */
using MemoryPack;

namespace LoneEftDmaRadar.Web.WebRadar.MemoryPack
{
    public sealed class Vector3Formatter : MemoryPackFormatter<Vector3>
    {
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Vector3 value)
        {
            writer.WriteUnmanaged(value.X, value.Y, value.Z);
        }

        public override void Deserialize(ref MemoryPackReader reader, scoped ref Vector3 value)
        {
            reader.ReadUnmanaged(out float x, out float y, out float z);
            value = new Vector3(x, y, z);
        }
    }
}
