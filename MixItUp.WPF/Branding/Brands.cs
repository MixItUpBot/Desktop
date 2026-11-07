using MixItUp.Base.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MixItUp.WPF.Branding
{
    /// <summary>
    /// One rendering mode of a brand mark (designed for dark or light backgrounds), exposing the
    /// staged image resources by size (Small = 64px, Medium = 128px) as XAML-ready ImageSources.
    /// </summary>
    public sealed class BrandImageVariant
    {
        public string SmallPath { get; private set; }

        public string MediumPath { get; private set; }

        private ImageSource small;
        private ImageSource medium;

        internal BrandImageVariant(string smallPath, string mediumPath)
        {
            this.SmallPath = smallPath;
            this.MediumPath = mediumPath;
        }

        public ImageSource Small
        {
            get
            {
                if (this.small == null)
                {
                    this.small = LoadImage(this.SmallPath);
                }
                return this.small;
            }
        }

        public ImageSource Medium
        {
            get
            {
                if (this.medium == null)
                {
                    this.medium = LoadImage(this.MediumPath);
                }
                return this.medium;
            }
        }

        private static ImageSource LoadImage(string resourcePath)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,," + resourcePath);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }

    /// <summary>
    /// A set of brand mark images (full-color Symbol or monochrome Mono) with variants tuned for
    /// dark and light backgrounds. Current resolves the variant matching the active app theme.
    /// </summary>
    public sealed class BrandImageSet
    {
        public BrandImageVariant OnDark { get; private set; }

        public BrandImageVariant OnLight { get; private set; }

        internal BrandImageSet(BrandImageVariant onDark, BrandImageVariant onLight)
        {
            this.OnDark = onDark;
            this.OnLight = onLight;
        }

        public BrandImageVariant Current { get { return Brands.IsDarkTheme ? this.OnDark : this.OnLight; } }

        /// <summary>
        /// Resolves the variant for primary-colored surfaces (e.g. GroupBox/Expander headers), where the
        /// effective foreground comes from the primary palette rather than the light/dark theme mode.
        /// </summary>
        public BrandImageVariant OnPrimary { get { return Brands.IsPrimarySurfaceDark ? this.OnDark : this.OnLight; } }
    }

    /// <summary>
    /// A third-party (or first-party) brand used by Mix It Up services and features, mirroring
    /// Website/src/data/brands.json. Image resources follow the Media/Brands naming convention:
    /// {id}_{color|mono}-{dark|light}_{sm|md}.png staged under Assets/Images/Brands.
    /// </summary>
    public sealed class Brand
    {
        private const string BrandImageRoot = "/Assets/Images/Brands/";

        public string Id { get; private set; }

        public string Name { get; private set; }

        public string Url { get; private set; }

        public bool HasBrandColors { get; private set; }

        public Color PrimaryColor { get; private set; }

        public Color SecondaryColor { get; private set; }

        public Color TertiaryColor { get; private set; }

        public SolidColorBrush PrimaryBrush { get; private set; }

        public SolidColorBrush SecondaryBrush { get; private set; }

        public SolidColorBrush TertiaryBrush { get; private set; }

        /// <summary>The full-color brand mark.</summary>
        public BrandImageSet Symbol { get; private set; }

        /// <summary>The monochrome brand mark.</summary>
        public BrandImageSet Mono { get; private set; }

        internal Brand(string id, string name, string url, string primary = null, string secondary = null, string tertiary = null)
        {
            this.Id = id;
            this.Name = name;
            this.Url = url;

            this.HasBrandColors = !string.IsNullOrEmpty(primary);
            this.PrimaryColor = ParseColor(primary);
            this.SecondaryColor = ParseColor(secondary);
            this.TertiaryColor = ParseColor(tertiary);
            this.PrimaryBrush = CreateBrush(this.PrimaryColor);
            this.SecondaryBrush = CreateBrush(this.SecondaryColor);
            this.TertiaryBrush = CreateBrush(this.TertiaryColor);

            this.Symbol = new BrandImageSet(this.CreateVariant("color-dark"), this.CreateVariant("color-light"));
            this.Mono = new BrandImageSet(this.CreateVariant("mono-dark"), this.CreateVariant("mono-light"));
        }

        private BrandImageVariant CreateVariant(string variant)
        {
            return new BrandImageVariant(
                smallPath: $"{BrandImageRoot}{this.Id}_{variant}_sm.png",
                mediumPath: $"{BrandImageRoot}{this.Id}_{variant}_md.png");
        }

        private static Color ParseColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex))
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(hex);
                }
                catch (FormatException) { }
            }
            return Color.FromRgb(0x9E, 0x9E, 0x9E);
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// Static registry of the brands used across Mix It Up services and features. Names, URLs, and
    /// colors mirror Website/src/data/brands.json.
    /// </summary>
    public static class Brands
    {
        public static readonly Brand MixItUp = new Brand("mix-it-up", "Mix It Up", "https://mixitup.bot", "#4865EE", "#2E43AE", "#101223");

        // Streaming platforms
        public static readonly Brand Twitch = new Brand("twitch", "Twitch", "https://twitch.tv", "#9146FF", "#772CE8", "#392E5C");
        public static readonly Brand YouTube = new Brand("youtube", "YouTube", "https://youtube.com", "#FF0000", "#CC0000", "#282828");
        public static readonly Brand Kick = new Brand("kick", "Kick", "https://kick.com", "#53FC18", "#3DB30F", "#0E0E10");
        public static readonly Brand Velora = new Brand("velora", "Velora", "https://velora.tv", "#FDCB16", "#D18829", "#581012");
        public static readonly Brand VPZone = new Brand("vpzone", "VPZone", "https://vpzone.tv", "#7C3AED", "#5B21B6", "#120B1F");

        // Streaming software
        public static readonly Brand OBSStudio = new Brand("obs", "OBS Studio", "https://obsproject.com", "#302E31", "#1F1E1F", "#000000");
        public static readonly Brand Streamlabs = new Brand("streamlabs", "Streamlabs", "https://streamlabs.com", "#80F5D2", "#31C9B0", "#0F2A3F");
        public static readonly Brand MeldStudio = new Brand("meld-studio", "Meld Studio", "https://meldstudio.co", "#FF6B35", "#E55A2B", "#1C1C1C");
        public static readonly Brand VelumStudio = new Brand("velum", "Velum Studio", "https://velumstudio.dev");
        public static readonly Brand XSplit = new Brand("xsplit", "XSplit", "https://www.xsplit.com", "#0095FF", "#0073C7", "#1C1C1C");

        // Donations & charity
        public static readonly Brand StreamElements = new Brand("streamelements", "StreamElements", "https://streamelements.com", "#F2613D", "#D14B2D", "#0E0F11");
        public static readonly Brand TipeeeStream = new Brand("tipeeestream", "TipeeeStream", "https://www.tipeeestream.com", "#33A4DD", "#1F7AA8", "#1A1A1A");
        public static readonly Brand TreatStream = new Brand("treatstream", "TreatStream", "https://treatstream.com", "#F9A825", "#F57F17", "#1A1A1A");
        public static readonly Brand Rainmaker = new Brand("rainmaker", "Rainmaker", "https://rainmaker.gg", "#00BFA5", "#00897B", "#1A1A1A");
        public static readonly Brand JustGiving = new Brand("justgiving", "JustGiving", "https://www.justgiving.com", "#AD1AAC", "#831183", "#1A1A1A");
        public static readonly Brand DonorDrive = new Brand("donor-drive", "DonorDrive", "https://donordrive.com", "#0084C7", "#005F8F", "#1A1A1A");
        public static readonly Brand Tiltify = new Brand("tiltify", "Tiltify", "https://tiltify.com", "#1F69FF", "#0D4DC7", "#1A1A1A");
        public static readonly Brand Patreon = new Brand("patreon", "Patreon", "https://www.patreon.com", "#FF424D", "#D8323A", "#052D49");
        public static readonly Brand KoFi = new Brand("kofi", "Ko-fi", "https://ko-fi.com");
        public static readonly Brand Fourthwall = new Brand("fourthwall", "Fourthwall", "https://fourthwall.com");
        public static readonly Brand Throne = new Brand("throne", "Throne", "https://throne.com");
        public static readonly Brand Pally = new Brand("pally", "Pally", "https://pally.gg");

        // Interactive & hardware
        public static readonly Brand Streamloots = new Brand("streamloots", "Streamloots", "https://www.streamloots.com", "#FF4747", "#D32F2F", "#1A1A1A");
        public static readonly Brand CrowdControl = new Brand("crowd-control", "Crowd Control", "https://crowdcontrol.live", "#FFC107", "#FFA000", "#1A1A1A");
        public static readonly Brand TITS = new Brand("tits", "TITS", "https://remasuri3.itch.io/tits", "#E91E63", "#AD1457", "#1A1A1A");
        public static readonly Brand Discord = new Brand("discord", "Discord", "https://discord.com", "#5865F2", "#404EED", "#23272A");
        public static readonly Brand IFTTT = new Brand("ifttt", "IFTTT", "https://ifttt.com", "#000000", "#333333", "#FFFFFF");
        public static readonly Brand SAMMI = new Brand("sammi", "SAMMI", "https://sammi.solutions", "#FF4081", "#C2185B", "#311B92");
        public static readonly Brand PolyPop = new Brand("polypop", "PolyPop", "https://polypop.live", "#A030F2", "#7C2BC4", "#1A0E2E");
        public static readonly Brand PixelChat = new Brand("pixel-chat", "PixelChat", "https://pixelchat.tv", "#FF5252", "#D32F2F", "#1A1A1A");
        public static readonly Brand VConnect = new Brand("vconnect", "VConnect", "https://github.com/Remasuri/VConnect_API", "#D25FD3", "#F79AF8", "#1A1A1A");
        public static readonly Brand LumiaStream = new Brand("lumia-stream", "Lumia Stream", "https://lumiastream.com", "#7C4DFF", "#5E35B1", "#1A1A1A");
        public static readonly Brand Voicemod = new Brand("voicemod", "Voicemod", "https://www.voicemod.net", "#FF3D71", "#C9184A", "#1A1A1A");
        public static readonly Brand Pulsoid = new Brand("pulsoid", "Pulsoid", "https://pulsoid.net", "#FF1744", "#D50000", "#1A1A1A");
        public static readonly Brand Arduino = new Brand("arduino", "Arduino", "https://www.arduino.cc", "#00979D", "#00626A", "#1A1A1A");
        public static readonly Brand Elgato = new Brand("elgato", "Elgato", "https://www.elgato.com/stream-deck", "#101820", "#3E4348", "#FFFFFF");
        public static readonly Brand Loupedeck = new Brand("loupedeck", "Loupedeck", "https://loupedeck.com", "#1F1F1F", "#3F3F3F", "#FFFFFF");

        // VTubing
        public static readonly Brand VTubeStudio = new Brand("vtube-studio", "VTube Studio", "https://denchisoft.com", "#00BCD4", "#0097A7", "#1A1A1A");
        public static readonly Brand VTSPog = new Brand("vts-pog", "VTS Pog", "https://github.com/RurioFurry/VTSPog", "#FF6E40", "#DD2C00", "#1A1A1A");
        public static readonly Brand MtionStudio = new Brand("mtion", "mtion studio", "https://mtionstudio.com", "#7C4DFF", "#5E35B1", "#1A1A1A");
        public static readonly Brand StreamAvatars = new Brand("stream-avatars", "Stream Avatars", "https://www.streamavatars.com", "#FF9800", "#F57C00", "#1A1A1A");
        public static readonly Brand Veadotube = new Brand("veadotube", "veadotube", "https://veado.tube", "#3F0000", "#FFFFFF", "#FFFFFF");
        public static readonly Brand RahiTuber = new Brand("rahituber", "RahiTuber", "https://rahisaurus.itch.io/rahituber", "#DB3AF3", "#A800BB", "#430047");

        // Text-to-speech
        public static readonly Brand AWS = new Brand("aws", "AWS", "https://aws.amazon.com/polly/", "#FF9900", "#EC7211", "#232F3E");
        public static readonly Brand GoogleCloud = new Brand("google-cloud", "Google Cloud", "https://cloud.google.com/text-to-speech", "#4285F4", "#34A853", "#1A1A1A");
        public static readonly Brand Azure = new Brand("azure", "Microsoft Azure", "https://azure.microsoft.com/services/cognitive-services/speech-services/", "#0078D4", "#005A9E", "#1A1A1A");
        public static readonly Brand ResponsiveVoice = new Brand("responsive-voice", "ResponsiveVoice", "https://responsivevoice.org", "#00BCD4", "#0097A7", "#1A1A1A");
        public static readonly Brand TTSMonster = new Brand("tts-monster", "TTS.Monster", "https://tts.monster", "#9C27B0", "#6A1B9A", "#1A1A1A");
        public static readonly Brand TikTok = new Brand("tiktok", "TikTok", "https://tiktok.com", "#FF0050", "#00F2EA", "#000000");

        // Emote providers
        public static readonly Brand BetterTTV = new Brand("betterttv", "BetterTTV", "https://betterttv.com", "#D50014", "#A30010", "#1A1A1A");
        public static readonly Brand FrankerFaceZ = new Brand("frankerface", "FrankerFaceZ", "https://www.frankerfacez.com");

        // Socials & community
        public static readonly Brand GitHub = new Brand("github", "GitHub", "https://github.com");
        public static readonly Brand Twitter = new Brand("twitter", "Twitter", "https://twitter.com", "#1DA1F2", "#0D8BD9", "#15202B");

        private static readonly Dictionary<string, Brand> lookup = new Dictionary<string, Brand>(StringComparer.OrdinalIgnoreCase);

        static Brands()
        {
            foreach (var field in typeof(Brands).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.GetValue(null) is Brand brand)
                {
                    lookup[brand.Id] = brand;
                }
            }
        }

        public static Brand Get(string id)
        {
            if (!string.IsNullOrEmpty(id) && lookup.TryGetValue(id, out Brand brand))
            {
                return brand;
            }
            return null;
        }

        public static IEnumerable<Brand> All { get { return lookup.Values; } }

        internal static bool IsDarkTheme
        {
            get
            {
                IThemeService themeService = ServiceManager.Get<IThemeService>();
                return themeService == null || themeService.IsDarkTheme;
            }
        }

        internal static bool IsPrimarySurfaceDark
        {
            get
            {
                if (Application.Current?.TryFindResource("MaterialDesign.Brush.Primary.Foreground") is SolidColorBrush brush)
                {
                    // A light foreground on the primary surface implies the surface reads as dark
                    Color color = brush.Color;
                    return ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255.0 > 0.5;
                }
                return IsDarkTheme;
            }
        }
    }
}
