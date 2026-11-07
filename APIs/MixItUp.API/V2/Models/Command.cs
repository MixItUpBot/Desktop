using System;

namespace MixItUp.API.V2.Models
{
    public enum CommandStateOptions
    {
        Disable = 0,
        Enable = 1,
        Toggle = 2,
    }

    public class CommandRoleRequirement
    {
        public string Platform { get; set; }
        public string Role { get; set; }
        public string[] Roles { get; set; } = Array.Empty<string>();
        public int SubscriberTier { get; set; }
        public string YouTubeMembershipLevelID { get; set; }
        public string PatreonBenefitID { get; set; }
    }

    public class Command
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsEnabled { get; set; }
        public bool Unlocked { get; set; }
        public string GroupName { get; set; }
        public string[] Triggers { get; set; } = Array.Empty<string>();
        public bool UsesWildcardTriggers { get; set; }
        public CommandRoleRequirement[] RoleRequirements { get; set; } = Array.Empty<CommandRoleRequirement>();
    }
}
