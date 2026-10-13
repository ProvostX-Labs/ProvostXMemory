using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Chunking;


/// <summary>
/// <para>Splits a record's content into embedded units for the dense index. Chunks carry
/// exact offsets back into the original content, so a chunk hit resolves to its parent
/// record — the whole record is always what surfaces in results, never the fragment.</para>
/// <para><b>Implementations:</b> <c>StructuredChunker</c> cuts on markdown structure first
/// (headings, paragraphs, then sentences) and only hard-slices oversized content;
/// <c>SizeChunker</c> packs by size with optional overlap — fixed-stride windows in
/// geometry mode, shared whole words in separator mode. Both preserve every character
/// of content across the split (mod whitespace at cut points).</para>
/// <para>Chunk ids are scope-qualified (<c>kind|scope-id|record-id|index</c>) — see
/// <c>ChunkIdCodec</c>; ids containing the '|' separator are rejected at compose time.</para>
/// </summary>
public interface IChunker
{
    IReadOnlyList<Chunk> Split(MemoryRecord record, ChunkOptions options);
}

