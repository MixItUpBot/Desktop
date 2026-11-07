#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace MixItUp.WPF.Services.Diagnostics;

public sealed record LocalIntegration(string Name, string[] Executables, string[] Folders, string[] SteamFolders, string Source);
public static class IntegrationCatalog
{
    public static readonly LocalIntegration[] All = [
        new("OBS Studio", [@"bin\64bit\obs64.exe","obs64.exe"], ["obs-studio"], [], "MixItUp.WPF/Controls/Services/OBSStudioServiceControl.xaml.cs"),
        new("Streamlabs Desktop", ["Streamlabs Desktop.exe","Streamlabs OBS.exe"], ["Streamlabs Desktop","Streamlabs OBS","slobs-client"], [], "MixItUp.Base/Services/External/StreamlabsDesktopService.cs"),
        new("Meld Studio", ["Meld.exe","Meld Studio.exe"], ["Meld","Meld Studio"], [], "MixItUp.Base/Services/External/MeldStudioService.cs"),
        new("XSplit", ["XSplit.Core.exe"], [@"SplitmediaLabs\XSplit Broadcaster","XSplit Broadcaster"], [], "MixItUp.Base/Services/External/XSplitService.cs"),
        new("VTube Studio", ["VTube Studio.exe"], ["VTube Studio"], ["VTube Studio"], "MixItUp.Base/Services/External/VTubeStudioService.cs"),
        new("VTSPog", ["VTSPog.exe","VTS P.O.G.exe"], ["VTSPog","VTS P.O.G"], ["VTS P.O.G."], "MixItUp.Base/Services/External/VTSPogService.cs"),
        new("VConnect", ["VConnect.exe"], ["VConnect"], [], "MixItUp.Base/Services/External/VConnectService.cs"),
        new("veadotube", ["veadotube mini.exe","veadotube.exe"], ["veadotube mini","veadotube"], [], "MixItUp.Base/Services/External/VeadotubeService.cs"),
        new("RahiTuber", ["RahiTuber.exe"], ["RahiTuber"], [], "MixItUp.Base/Services/External/RahiTuberService.cs"),
        new("T.I.T.S.", ["TITS.exe","Twitch Integrated Throwing System.exe"], ["TITS","Twitch Integrated Throwing System"], ["Twitch Integrated Throwing System"], "MixItUp.Base/Services/External/TITSService.cs"),
        new("PolyPop", ["PolyPop.exe"], ["PolyPop"], ["PolypopLive"], "MixItUp.Base/Services/External/PolyPopService.cs"),
        new("mtion studio", ["mtion studio.exe","mtion.exe"], ["mtion studio","mtion"], ["mtion studio"], "MixItUp.Base/Services/MtionStudioService.cs"),
        new("Voicemod", ["VoicemodDesktop.exe","Voicemod.exe"], ["Voicemod Desktop","Voicemod"], [], "MixItUp.Base/Services/External/VoicemodService.cs"),
        new("Lumia Stream", ["Lumia Stream.exe"], ["Lumia Stream","lumia-stream"], [], "MixItUp.Base/Services/External/LumiaStreamService.cs"),
        new("SAMMI", ["SAMMI Core.exe","SAMMI.exe"], ["SAMMI","SAMMI Core"], [], "MixItUp.Base/Services/External/SAMMIService.cs"),
        new("Stream Deck", ["StreamDeck.exe"], [@"Elgato\StreamDeck","StreamDeck"], [], "MixItUp.WPF/Controls/Services/StreamDeckServiceControl.xaml.cs"),
        new("Loupedeck", ["LoupedeckApp.exe"], ["Loupedeck"], [], "MixItUp.WPF/Controls/Services/LoupeDeckServiceControl.xaml.cs"),
        new("Stream Avatars", ["Stream Avatars.exe","StreamAvatars.exe"], ["Stream Avatars"], ["Stream Avatars"], "MixItUp.WPF/Controls/Services/StreamAvatarsServiceControl.xaml.cs"),
        new("Crowd Control", ["Crowd Control.exe","CrowdControl.exe"], ["Crowd Control","CrowdControl"], [], "MixItUp.Base/Services/External/CrowdControlService.cs")
    ];
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    public static LocalIntegration? Match(string product)
    {
        var normalized=Normalize(product);
        return All.FirstOrDefault(item=>new[]{item.Name}.Concat(item.Folders).Any(alias=>normalized.Contains(Normalize(alias),StringComparison.Ordinal)));
    }
}
