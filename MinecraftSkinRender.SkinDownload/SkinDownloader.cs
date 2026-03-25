// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.Json;
using MinecraftSkinRender.MojangApi;

public static class SkinDownloader
{
    public static async Task Download()
    {
        bool download = false;
        if (download)
        {
            Console.WriteLine("Download skin");

            var res = await MinecraftAPI.GetMinecraftProfileNameAsync("Color_yr");
            var res1 = await MinecraftAPI.GetUserProfile(res!.UUID);
            if (res1 == null)
            {
                Console.WriteLine("No skin data");
            }

            TexturesObj? obj = null;
            foreach (var item in res1.Properties)
            {
                if (item.Name == "textures")
                {
                    var temp = Convert.FromBase64String(item.Value);
                    var data = Encoding.UTF8.GetString(temp);
                    obj = JsonDocument.Parse(data).Deserialize<TexturesObj>();
                    break;
                }
            }

            if (obj == null)
            {
                Console.WriteLine("No have skin");
                return;
            }

            if (obj.Textures.Skin.Url != null)
            {
                var data = await MinecraftAPI.Client.GetByteArrayAsync(obj!.Textures.Skin.Url);
                File.WriteAllBytes("skin.png", data);
            }

            if (obj.Textures.Cape.Url != null)
            {
                var data = await MinecraftAPI.Client.GetByteArrayAsync(obj!.Textures.Cape.Url);
                File.WriteAllBytes("cape.png", data);
            }

            Console.WriteLine("Download skin done");
        }
    }
}