using UnityEngine;

namespace AudioRender {
  internal class GfxLine {
    public GfxLine(Vector2 start, Vector2 end, float intensity) {
      this.start     = start;
      this.end       = end;
      this.intensity = intensity;
    }

    public float   intensity { get; private set; }
    public Vector2 start     { get; private set; }
    public Vector2 end       { get; private set; }
  }
}