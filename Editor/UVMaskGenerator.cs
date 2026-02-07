using UnityEngine;
using System.Collections.Generic;

namespace MeshUVMaskGenerator
{
    public enum MaskChannel
    {
        Red,
        Green,
        Blue,
        Alpha,
        RGBA
    }
    public class UVMaskGenerator
    {
        public int TextureSize { get; set; } = 1024;
        public Color BackgroundColor { get; set; } = Color.white;
        public Color MaskColor { get; set; } = Color.black;
        public bool FillPolygons { get; set; } = true;
        public float LineThickness { get; set; } = 1.0f;
        public int DilationPixels { get; set; } = 0;
        public MaskChannel OutputChannel { get; set; } = MaskChannel.RGBA;
        public bool EnableGradient { get; set; } = false;
        public float GradientAngle { get; set; } = 0f;
        public float GradientStart { get; set; } = 0f;
        public float GradientEnd { get; set; } = 1f;

        public Texture2D GenerateUVMask(Mesh mesh, int[] triangles)
        {
            if (mesh == null || triangles == null || triangles.Length == 0)
                return null;

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            
            Color[] pixels = new Color[TextureSize * TextureSize];
            // Initialize based on output channel
            Color initialColor = (OutputChannel == MaskChannel.RGBA) ? BackgroundColor : Color.clear;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = initialColor;

            Vector2[] uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0)
            {
                texture.SetPixels(pixels);
                texture.Apply();
                return texture;
            }

            if (FillPolygons)
            {
                DrawFilledTriangles(pixels, uvs, triangles);
            }
            else
            {
                DrawWireframeTriangles(pixels, uvs, triangles);
            }

            if (DilationPixels > 0)
            {
                pixels = ApplyDilation(pixels, TextureSize, DilationPixels);
            }

            if (EnableGradient)
            {
                pixels = ApplyGradient(pixels, TextureSize);
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void DrawFilledTriangles(Color[] pixels, Vector2[] uvs, int[] triangles)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector2 uv0 = uvs[triangles[i]];
                Vector2 uv1 = uvs[triangles[i + 1]];
                Vector2 uv2 = uvs[triangles[i + 2]];
                FillTriangle(pixels, TextureSize, uv0, uv1, uv2, MaskColor);
            }
        }

        private void DrawWireframeTriangles(Color[] pixels, Vector2[] uvs, int[] triangles)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector2 uv0 = uvs[triangles[i]];
                Vector2 uv1 = uvs[triangles[i + 1]];
                Vector2 uv2 = uvs[triangles[i + 2]];

                DrawLine(pixels, TextureSize, uv0, uv1, MaskColor, LineThickness);
                DrawLine(pixels, TextureSize, uv1, uv2, MaskColor, LineThickness);
                DrawLine(pixels, TextureSize, uv2, uv0, MaskColor, LineThickness);
            }
        }

        private void FillTriangle(Color[] pixels, int size, Vector2 v0, Vector2 v1, Vector2 v2, Color color)
        {
            int x0 = Mathf.RoundToInt(v0.x * (size - 1));
            int y0 = Mathf.RoundToInt(v0.y * (size - 1));
            int x1 = Mathf.RoundToInt(v1.x * (size - 1));
            int y1 = Mathf.RoundToInt(v1.y * (size - 1));
            int x2 = Mathf.RoundToInt(v2.x * (size - 1));
            int y2 = Mathf.RoundToInt(v2.y * (size - 1));

            int minX = Mathf.Max(0, Mathf.Min(x0, x1, x2));
            int maxX = Mathf.Min(size - 1, Mathf.Max(x0, x1, x2));
            int minY = Mathf.Max(0, Mathf.Min(y0, y1, y2));
            int maxY = Mathf.Min(size - 1, Mathf.Max(y0, y1, y2));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (IsPointInTriangle(x, y, x0, y0, x1, y1, x2, y2))
                    {
                        int index = y * size + x;
                        pixels[index] = ApplyChannelMask(pixels[index], color);
                    }
                }
            }
        }

        private bool IsPointInTriangle(int px, int py, int x0, int y0, int x1, int y1, int x2, int y2)
        {
            float area = 0.5f * Mathf.Abs((x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0));
            float area0 = 0.5f * Mathf.Abs((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py));
            float area1 = 0.5f * Mathf.Abs((px - x0) * (y2 - y0) - (x2 - x0) * (py - y0));
            float area2 = 0.5f * Mathf.Abs((x1 - x0) * (py - y0) - (px - x0) * (y1 - y0));
            return Mathf.Abs(area - (area0 + area1 + area2)) < 0.01f;
        }

        private void DrawLine(Color[] pixels, int size, Vector2 start, Vector2 end, Color color, float thickness)
        {
            int x0 = Mathf.RoundToInt(start.x * (size - 1));
            int y0 = Mathf.RoundToInt(start.y * (size - 1));
            int x1 = Mathf.RoundToInt(end.x * (size - 1));
            int y1 = Mathf.RoundToInt(end.y * (size - 1));

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                DrawThickPixel(pixels, size, x0, y0, color, thickness);

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void DrawThickPixel(Color[] pixels, int size, int x, int y, Color color, float thickness)
        {
            int radius = Mathf.CeilToInt(thickness / 2.0f);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        int px = x + dx;
                        int py = y + dy;

                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            int index = py * size + px;
                            pixels[index] = ApplyChannelMask(pixels[index], color);
                        }
                    }
                }
            }
        }

        private Color[] ApplyDilation(Color[] originalPixels, int size, int dilationPixels)
        {
            if (dilationPixels <= 0) return originalPixels;

            Color effectiveBg = GetEffectiveBackgroundColor();

            // Distance transform approach - much more efficient
            float[] distanceMap = new float[originalPixels.Length];

            // Initialize distance map
            for (int i = 0; i < originalPixels.Length; i++)
            {
                distanceMap[i] = IsBackgroundColor(originalPixels[i], effectiveBg) ? float.MaxValue : 0f;
            }

            // Forward pass
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    
                    if (distanceMap[index] > 0)
                    {
                        // Check left and top neighbors
                        if (x > 0)
                        {
                            float leftDist = distanceMap[index - 1] + 1f;
                            if (leftDist < distanceMap[index])
                                distanceMap[index] = leftDist;
                        }
                        
                        if (y > 0)
                        {
                            float topDist = distanceMap[index - size] + 1f;
                            if (topDist < distanceMap[index])
                                distanceMap[index] = topDist;
                        }
                    }
                }
            }

            // Backward pass
            for (int y = size - 1; y >= 0; y--)
            {
                for (int x = size - 1; x >= 0; x--)
                {
                    int index = y * size + x;
                    
                    if (distanceMap[index] > 0)
                    {
                        // Check right and bottom neighbors
                        if (x < size - 1)
                        {
                            float rightDist = distanceMap[index + 1] + 1f;
                            if (rightDist < distanceMap[index])
                                distanceMap[index] = rightDist;
                        }
                        
                        if (y < size - 1)
                        {
                            float bottomDist = distanceMap[index + size] + 1f;
                            if (bottomDist < distanceMap[index])
                                distanceMap[index] = bottomDist;
                        }
                    }
                }
            }

            // Create result based on distance threshold
            Color[] result = new Color[originalPixels.Length];
            for (int i = 0; i < originalPixels.Length; i++)
            {
                if (distanceMap[i] <= dilationPixels)
                {
                    result[i] = ApplyChannelMask(originalPixels[i], MaskColor);
                }
                else
                {
                    result[i] = ApplyChannelMask(originalPixels[i], BackgroundColor);
                }
            }

            return result;
        }

        private Color[] ApplyGradient(Color[] pixels, int size)
        {
            float rad = GradientAngle * Mathf.Deg2Rad;
            float dirX = Mathf.Cos(rad);
            float dirY = Mathf.Sin(rad);

            Color effectiveBg = GetEffectiveBackgroundColor();

            // Find projection min/max over mask pixels only
            float projMin = float.MaxValue;
            float projMax = float.MinValue;
            bool hasMask = false;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    if (!IsBackgroundColor(pixels[index], effectiveBg))
                    {
                        float proj = x * dirX + y * dirY;
                        if (proj < projMin) projMin = proj;
                        if (proj > projMax) projMax = proj;
                        hasMask = true;
                    }
                }
            }

            if (!hasMask || Mathf.Approximately(projMin, projMax))
                return pixels;

            float range = projMax - projMin;

            Color[] result = new Color[pixels.Length];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    if (IsBackgroundColor(pixels[index], effectiveBg))
                    {
                        result[index] = pixels[index];
                    }
                    else
                    {
                        float proj = x * dirX + y * dirY;
                        float tRaw = (proj - projMin) / range;
                        float gradientRange = GradientEnd - GradientStart;
                        float t = gradientRange > 0f
                            ? Mathf.Clamp01((tRaw - GradientStart) / gradientRange)
                            : (tRaw >= GradientEnd ? 1f : 0f);

                        switch (OutputChannel)
                        {
                            case MaskChannel.Red:
                                result[index] = Color.Lerp(Color.clear, new Color(1f, 0f, 0f, 1f), t);
                                break;
                            case MaskChannel.Green:
                                result[index] = Color.Lerp(Color.clear, new Color(0f, 1f, 0f, 1f), t);
                                break;
                            case MaskChannel.Blue:
                                result[index] = Color.Lerp(Color.clear, new Color(0f, 0f, 1f, 1f), t);
                                break;
                            case MaskChannel.Alpha:
                                result[index] = Color.Lerp(Color.clear, new Color(0f, 0f, 0f, 1f), t);
                                break;
                            case MaskChannel.RGBA:
                            default:
                                result[index] = Color.Lerp(BackgroundColor, MaskColor, t);
                                break;
                        }
                    }
                }
            }

            return result;
        }

        private Color GetEffectiveBackgroundColor()
        {
            return (OutputChannel == MaskChannel.RGBA) ? BackgroundColor : Color.clear;
        }

        private bool IsBackgroundColor(Color color)
        {
            return IsBackgroundColor(color, BackgroundColor);
        }

        private bool IsBackgroundColor(Color color, Color referenceBackground)
        {
            float threshold = 0.01f;
            return Mathf.Abs(color.r - referenceBackground.r) < threshold &&
                   Mathf.Abs(color.g - referenceBackground.g) < threshold &&
                   Mathf.Abs(color.b - referenceBackground.b) < threshold &&
                   Mathf.Abs(color.a - referenceBackground.a) < threshold;
        }

        private Color ApplyChannelMask(Color baseColor, Color maskColor)
        {
            switch (OutputChannel)
            {
                case MaskChannel.Red:
                    // Red channel only: white(1.0) for mask, transparent for background
                    if (IsBackgroundColor(maskColor))
                        return Color.clear; // Transparent background
                    else
                        return new Color(1f, 0f, 0f, 1f); // Red mask
                case MaskChannel.Green:
                    if (IsBackgroundColor(maskColor))
                        return Color.clear;
                    else
                        return new Color(0f, 1f, 0f, 1f); // Green mask
                case MaskChannel.Blue:
                    if (IsBackgroundColor(maskColor))
                        return Color.clear;
                    else
                        return new Color(0f, 0f, 1f, 1f); // Blue mask
                case MaskChannel.Alpha:
                    if (IsBackgroundColor(maskColor))
                        return Color.clear;
                    else
                        return new Color(0f, 0f, 0f, 1f); // Opaque mask
                case MaskChannel.RGBA:
                default:
                    // Full color mode uses the actual colors
                    return maskColor;
            }
        }
    }
}