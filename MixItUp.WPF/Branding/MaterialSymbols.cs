using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace MixItUp.WPF.Branding
{
    /// <summary>
    /// Material Design 3 (Material Symbols Sharp) icon glyphs, matching the icon library used by the
    /// Mix It Up website. Codepoints come from the google/material-design-icons codepoints file and are
    /// rendered with the embedded Assets\Fonts\MaterialSymbolsSharp-Filled.ttf resource font.
    /// </summary>
    public static class MaterialSymbols
    {
        public static readonly FontFamily Font = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Material Symbols Sharp");

        public const string Api = "\uF1B7";
        public const string Badge = "\uEA67";
        public const string BarChart = "\uE26B";
        public const string BugReport = "\uE868";
        public const string CardGiftcard = "\uE8F6";
        public const string Casino = "\uEB40";
        public const string Category = "\uE72C";
        public const string Chat = "\uE0C9";
        public const string CloudDownload = "\uE2C0";
        public const string Construction = "\uEA3C";
        public const string Dashboard = "\uE871";
        public const string DataObject = "\uEAD3";
        public const string FlashOn = "\uE3E7";
        public const string FormatQuote = "\uE244";
        public const string Forum = "\uE8AF";
        public const string GraphicEq = "\uE1B8";
        public const string History = "\uE8B3";
        public const string Hub = "\uE9F4";
        public const string Info = "\uE88E";
        public const string Inventory = "\uE179";
        public const string Inventory2 = "\uE1A1";
        public const string Layers = "\uE53B";
        public const string Leaderboard = "\uF20C";
        public const string Loyalty = "\uE89A";
        public const string ManageAccounts = "\uF02E";
        public const string MusicNote = "\uE405";
        public const string NewReleases = "\uEF76";
        public const string Paid = "\uF041";
        public const string People = "\uEA21";
        public const string QueuePlayNext = "\uE066";
        public const string RecordVoiceOver = "\uE91F";
        public const string Rule = "\uF1C2";
        public const string Sdk = "\uE720";
        public const string Security = "\uE32A";
        public const string Settings = "\uE8B8";
        public const string ShoppingBasket = "\uE8CB";
        public const string Storefront = "\uEA12";
        public const string Timer = "\uE425";
        public const string Tv = "\uE63B";
        public const string VolunteerActivism = "\uEA70";
        public const string Webhook = "\uEB92";
        public const string WorkspacePremium = "\uE7AF";

        private static readonly Dictionary<string, string> iconNameToGlyph = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "api", Api },
            { "badge", Badge },
            { "bar_chart", BarChart },
            { "bug_report", BugReport },
            { "card_giftcard", CardGiftcard },
            { "casino", Casino },
            { "category", Category },
            { "chat", Chat },
            { "cloud_download", CloudDownload },
            { "construction", Construction },
            { "dashboard", Dashboard },
            { "data_object", DataObject },
            { "flash_on", FlashOn },
            { "format_quote", FormatQuote },
            { "forum", Forum },
            { "graphic_eq", GraphicEq },
            { "history", History },
            { "hub", Hub },
            { "info", Info },
            { "inventory", Inventory },
            { "inventory_2", Inventory2 },
            { "layers", Layers },
            { "leaderboard", Leaderboard },
            { "loyalty", Loyalty },
            { "manage_accounts", ManageAccounts },
            { "music_note", MusicNote },
            { "new_releases", NewReleases },
            { "paid", Paid },
            { "people", People },
            { "queue_play_next", QueuePlayNext },
            { "record_voice_over", RecordVoiceOver },
            { "rule", Rule },
            { "sdk", Sdk },
            { "security", Security },
            { "settings", Settings },
            { "shopping_basket", ShoppingBasket },
            { "storefront", Storefront },
            { "timer", Timer },
            { "tv", Tv },
            { "volunteer_activism", VolunteerActivism },
            { "webhook", Webhook },
            { "workspace_premium", WorkspacePremium },

            // Candidates for the PackIcon (mdi) -> Material Symbols migration; previewed on the
            // debug-only Icon Map page (IconMapControl).
            { "accessibility_new", "\uE92C" },
            { "add", "\uE145" },
            { "add_box", "\uE146" },
            { "add_moderator", "\uE97D" },
            { "admin_panel_settings", "\uEF3D" },
            { "apps", "\uE5C3" },
            { "arrow_back", "\uE5C4" },
            { "arrow_circle_left", "\uEAA7" },
            { "arrow_downward", "\uE5DB" },
            { "arrow_forward", "\uE5C8" },
            { "arrow_right_alt", "\uE941" },
            { "arrow_upward", "\uE5D8" },
            { "aspect_ratio", "\uE85B" },
            { "attach_money", "\uE227" },
            { "audio_file", "\uEB82" },
            { "block", "\uF08C" },
            { "broadcast_on_personal", "\uF8F9" },
            { "calendar_month", "\uEBCC" },
            { "cancel", "\uE888" },
            { "cell_tower", "\uEBBA" },
            { "center_focus_strong", "\uE3B4" },
            { "check_circle", "\uF0BE" },
            { "chevron_left", "\uE5CB" },
            { "chevron_right", "\uE5CC" },
            { "cleaning_services", "\uF0FF" },
            { "close", "\uE5CD" },
            { "cloud_off", "\uE2C1" },
            { "code", "\uE86F" },
            { "contact_support", "\uE94C" },
            { "content_copy", "\uE14D" },
            { "coronavirus", "\uF221" },
            { "dangerous", "\uE99A" },
            { "delete", "\uE92E" },
            { "delete_forever", "\uE92B" },
            { "delete_sweep", "\uE16C" },
            { "download", "\uF090" },
            { "drag_handle", "\uE25D" },
            { "drag_indicator", "\uE945" },
            { "east", "\uF1DF" },
            { "edit", "\uF097" },
            { "edit_note", "\uE745" },
            { "emoji_people", "\uEA1D" },
            { "error", "\uF8B6" },
            { "event", "\uE878" },
            { "exit_to_app", "\uE879" },
            { "expand_less", "\uE5CE" },
            { "expand_more", "\uE5CF" },
            { "file_copy", "\uE173" },
            { "file_download", "\uF090" },
            { "filter_center_focus", "\uE3DC" },
            { "find_replace", "\uE881" },
            { "folder", "\uE2C7" },
            { "folder_copy", "\uEBBD" },
            { "folder_open", "\uE2C8" },
            { "format_align_center", "\uE234" },
            { "format_align_justify", "\uE235" },
            { "format_align_left", "\uE236" },
            { "format_align_right", "\uE237" },
            { "format_bold", "\uE238" },
            { "format_italic", "\uE23F" },
            { "format_list_numbered", "\uE242" },
            { "format_underlined", "\uE249" },
            { "gpp_bad", "\uF012" },
            { "group", "\uEA21" },
            { "groups", "\uF233" },
            { "help", "\uE8FD" },
            { "input", "\uE890" },
            { "keep", "\uF027" },
            { "keep_off", "\uE6F9" },
            { "key", "\uE73C" },
            { "keyboard_arrow_down", "\uE313" },
            { "keyboard_arrow_up", "\uE316" },
            { "launch", "\uE89E" },
            { "library_add", "\uE03C" },
            { "library_music", "\uE030" },
            { "link", "\uE250" },
            { "link_off", "\uE16F" },
            { "local_police", "\uEF56" },
            { "lock", "\uE899" },
            { "lock_open", "\uE898" },
            { "manage_search", "\uF02F" },
            { "mop", "\uE28D" },
            { "more_vert", "\uE5D4" },
            { "music_note_add", "\uF391" },
            { "north", "\uF1E0" },
            { "notifications", "\uE7F5" },
            { "open_in_full", "\uF1CE" },
            { "open_in_new", "\uE89E" },
            { "output", "\uEBBE" },
            { "passkey", "\uF87F" },
            { "payments", "\uEF63" },
            { "play_arrow", "\uE037" },
            { "play_pause", "\uF137" },
            { "playlist_add", "\uE03B" },
            { "push_pin", "\uF10D" },
            { "queue_music", "\uE03D" },
            { "rate_review", "\uE560" },
            { "redeem", "\uE8F6" },
            { "refresh", "\uE5D5" },
            { "remove_moderator", "\uE9D4" },
            { "reorder", "\uE8FE" },
            { "repeat", "\uE040" },
            { "repeat_on", "\uE9D6" },
            { "repeat_one", "\uE041" },
            { "replay", "\uE042" },
            { "satellite_alt", "\uEB3A" },
            { "save", "\uE161" },
            { "savings", "\uE2EB" },
            { "schedule", "\uEFD6" },
            { "search", "\uEF7A" },
            { "select_window", "\uE6FA" },
            { "sensors", "\uE51E" },
            { "settings_input_antenna", "\uE8BF" },
            { "shield", "\uE9E0" },
            { "shield_person", "\uF650" },
            { "shuffle", "\uE043" },
            { "shuffle_on", "\uE9E1" },
            { "signal_disconnected", "\uF239" },
            { "skip_next", "\uE044" },
            { "skip_previous", "\uE045" },
            { "skull", "\uF89A" },
            { "sms", "\uE625" },
            { "south", "\uF1E3" },
            { "star", "\uF09A" },
            { "stop", "\uE047" },
            { "swap_horiz", "\uE8D4" },
            { "sync", "\uE627" },
            { "task_alt", "\uE2E6" },
            // Supplementary-plane codepoints (e.g. terminal_2 at U+FFF8E) fail to render through WPF's
            // composite font lookup even though the glyph exists in the font; stick to BMP icons.
            { "terminal", "\uEB8E" },
            { "text_fields", "\uE262" },
            { "trending_flat", "\uE8E4" },
            { "upload", "\uF09B" },
            { "upload_file", "\uE9FC" },
            { "verified_user", "\uF013" },
            { "vpn_key", "\uE0DA" },
            { "warning", "\uF083" },
            { "waving_hand", "\uE766" },
            { "wifi_tethering", "\uE1E2" },
        };

        /// <summary>
        /// Resolves a Material Symbols icon name (e.g. "flash_on") to its font glyph. Returns null when
        /// the icon name is unknown or empty.
        /// </summary>
        public static string GetGlyph(string iconName)
        {
            if (!string.IsNullOrEmpty(iconName) && iconNameToGlyph.TryGetValue(iconName, out string glyph))
            {
                return glyph;
            }
            return null;
        }
    }
}
