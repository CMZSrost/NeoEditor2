using System;

namespace NeoEditor.Plugins.ImageTools.Helper;

public readonly record struct PixelArtOutputSize(int Width, int Height)
{
    public int X2Width => Width * PixelArtOutputSizeCalculator.X2Scale;
    public int X2Height => Height * PixelArtOutputSizeCalculator.X2Scale;
}

public static class PixelArtOutputSizeCalculator
{
    public const int BaseStep = 10;
    public const int X2Scale = 2;

    public static PixelArtOutputSize NormalizeUnlocked(int width, int height)
    {
        return new PixelArtOutputSize(SnapToBaseStep(width), SnapToBaseStep(height));
    }

    public static PixelArtOutputSize ResolveFromWidth(int width, double aspectRatio)
    {
        var snappedWidth = SnapToBaseStep(width);
        if (!IsValidAspectRatio(aspectRatio))
        {
            return new PixelArtOutputSize(snappedWidth, SnapToBaseStep(snappedWidth));
        }

        var idealHeight = snappedWidth / aspectRatio;
        return new PixelArtOutputSize(snappedWidth,
            SnapToBaseStep((int)Math.Round(idealHeight, MidpointRounding.AwayFromZero)));
    }

    public static PixelArtOutputSize ResolveFromHeight(int height, double aspectRatio)
    {
        var snappedHeight = SnapToBaseStep(height);
        if (!IsValidAspectRatio(aspectRatio))
        {
            return new PixelArtOutputSize(SnapToBaseStep(snappedHeight), snappedHeight);
        }

        var idealWidth = snappedHeight * aspectRatio;
        return new PixelArtOutputSize(SnapToBaseStep((int)Math.Round(idealWidth, MidpointRounding.AwayFromZero)),
            snappedHeight);
    }

    public static PixelArtOutputSize ResolveNearest(int preferredWidth, int preferredHeight, double aspectRatio)
    {
        var unlocked = NormalizeUnlocked(preferredWidth, preferredHeight);
        if (!IsValidAspectRatio(aspectRatio))
        {
            return unlocked;
        }

        PixelArtOutputSize? best = null;
        double bestRatioError = double.MaxValue;
        var bestDistance = int.MaxValue;

        EvaluateCandidate(ResolveFromWidth(unlocked.Width, aspectRatio));
        EvaluateCandidate(ResolveFromWidth(Math.Max(BaseStep, unlocked.Width - BaseStep), aspectRatio));
        EvaluateCandidate(ResolveFromWidth(unlocked.Width + BaseStep, aspectRatio));
        EvaluateCandidate(ResolveFromHeight(unlocked.Height, aspectRatio));
        EvaluateCandidate(ResolveFromHeight(Math.Max(BaseStep, unlocked.Height - BaseStep), aspectRatio));
        EvaluateCandidate(ResolveFromHeight(unlocked.Height + BaseStep, aspectRatio));

        return best ?? unlocked;

        void EvaluateCandidate(PixelArtOutputSize candidate)
        {
            var candidateAspectRatio = candidate.Height <= 0 ? 0D : candidate.Width / (double)candidate.Height;
            var ratioError = Math.Abs(candidateAspectRatio - aspectRatio);
            var distance = Math.Abs(candidate.Width - preferredWidth) + Math.Abs(candidate.Height - preferredHeight);

            if (ratioError < bestRatioError - 1e-9 ||
                (Math.Abs(ratioError - bestRatioError) <= 1e-9 && distance < bestDistance))
            {
                best = candidate;
                bestRatioError = ratioError;
                bestDistance = distance;
            }
        }
    }

    public static int SnapToBaseStep(int value)
    {
        var safeValue = Math.Max(BaseStep, value);
        return Math.Max(BaseStep,
            (int)Math.Round(safeValue / (double)BaseStep, MidpointRounding.AwayFromZero) * BaseStep);
    }

    private static bool IsValidAspectRatio(double aspectRatio)
    {
        return aspectRatio > 0 && !double.IsNaN(aspectRatio) && !double.IsInfinity(aspectRatio);
    }
}
