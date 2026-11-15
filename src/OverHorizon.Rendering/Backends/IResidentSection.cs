using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// The backend-agnostic shape of an uploaded 16x16x16 chunk section. Both the OpenGL and the Vulkan backends
/// implement it, which is what lets the multi-threaded frustum culling stage stay completely free of graphics
/// API calls and be shared by every backend.
/// </summary>
internal interface IResidentSection
{
    /// <summary>Gets the 3D section coordinate.</summary>
    SectionPos SectionPosition { get; }

    /// <summary>Gets the world-space 16x16x16 bounds of the section, used for frustum culling.</summary>
    BoundingBox WorldBounds { get; }

    /// <summary>Gets the number of indices in the opaque draw batch (<c>0</c> when the section is fully see-through).</summary>
    int OpaqueIndexCount { get; }

    /// <summary>Gets the number of indices in the translucent draw batch.</summary>
    int TranslucentIndexCount { get; }
}
