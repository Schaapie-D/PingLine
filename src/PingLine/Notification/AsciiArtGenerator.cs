using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PingLine.Notification;

public static class AsciiArtGenerator
{
    private static readonly Dictionary<string, string[]> Cache = new();

    public static async Task<string[]> GenerateFromUrl(string url, int outputHeight = 30)
    {
        if(Cache.TryGetValue(url, out var art)) return art;

        using HttpClient client = new HttpClient();
        byte[] data = await client.GetByteArrayAsync(url);

        using var image = Image.Load<Rgba32>(data);
        art = GetArt(image, outputHeight);
        Cache[url] = art;
        return art;
    }

    static string[] GetArt(Image<Rgba32> image, int height)
    {
        var rows = new List<string>();

        int width = (int)(image.Width / (double)image.Height * height * 2.0);
        image.Mutate(x => x.Resize(width, height * 2));

        for (int y = 0; y < image.Height; y += 2)
        {
            var sb = new StringBuilder();

            for (int x = 0; x < image.Width; x++)
            {
                Rgba32 top = image[x, y];
                Rgba32 bottom = (y + 1 < image.Height) ? image[x, y + 1] : new Rgba32(0, 0, 0);

                sb.Append($"\x1b[38;2;{top.R};{top.G};{top.B}m"); // Set foreground color
                sb.Append($"\x1b[48;2;{bottom.R};{bottom.G};{bottom.B}m"); // Set background color
                sb.Append("▀");
            }

            sb.Append("\x1b[0m");

            rows.Add(sb.ToString());
        }

        return rows.ToArray();
    }
}