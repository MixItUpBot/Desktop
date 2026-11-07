using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class VeloraActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Velora; } }

        public IEnumerable<VeloraActionType> ActionTypes { get { return EnumHelper.GetEnumList<VeloraActionType>(); } }

        public IEnumerable<VeloraAnnounceColor> AnnounceColors { get { return EnumHelper.GetEnumList<VeloraAnnounceColor>(); } }

        public VeloraActionType SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged(nameof(this.ShowPoll));
                if (this.ShowPoll && string.IsNullOrEmpty(this.Amount)) { this.Amount = "120"; }
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.ShowText));
                this.NotifyPropertyChanged(nameof(this.TextHint));
                this.NotifyPropertyChanged(nameof(this.ShowTargetUser));
                this.NotifyPropertyChanged(nameof(this.ShowAmount));
                this.NotifyPropertyChanged(nameof(this.AmountHint));
                this.NotifyPropertyChanged(nameof(this.ShowReason));
                this.NotifyPropertyChanged(nameof(this.ShowColor));
                this.NotifyPropertyChanged(nameof(this.ShowSendAsStreamer));
            }
        }
        private VeloraActionType selectedActionType;

        public string Text
        {
            get { return this.text; }
            set { this.text = value; this.NotifyPropertyChanged(); }
        }
        private string text;

        public string TargetUsername
        {
            get { return this.targetUsername; }
            set { this.targetUsername = value; this.NotifyPropertyChanged(); }
        }
        private string targetUsername;

        public string Amount
        {
            get { return this.amount; }
            set { this.amount = value; this.NotifyPropertyChanged(); }
        }
        private string amount;

        public bool ShowPoll => this.SelectedActionType == VeloraActionType.CreatePoll;
        public string PollOptions { get { return this.pollOptions; } set { this.pollOptions = value; this.NotifyPropertyChanged(); } }
        private string pollOptions;
        public bool PollMultiple { get { return this.pollMultiple; } set { this.pollMultiple = value; this.NotifyPropertyChanged(); } }
        private bool pollMultiple;

        public string Reason
        {
            get { return this.reason; }
            set { this.reason = value; this.NotifyPropertyChanged(); }
        }
        private string reason;

        public VeloraAnnounceColor SelectedAnnounceColor
        {
            get { return this.selectedAnnounceColor; }
            set { this.selectedAnnounceColor = value; this.NotifyPropertyChanged(); }
        }
        private VeloraAnnounceColor selectedAnnounceColor;

        public bool SendAsStreamer
        {
            get { return this.sendAsStreamer; }
            set { this.sendAsStreamer = value; this.NotifyPropertyChanged(); }
        }
        private bool sendAsStreamer;

        // Field visibility per action type.
        public bool ShowText
        {
            get
            {
                return this.SelectedActionType == VeloraActionType.SetTitle
                    || this.SelectedActionType == VeloraActionType.SetGame
                    || this.SelectedActionType == VeloraActionType.Announce
                    || this.SelectedActionType == VeloraActionType.Raid || this.ShowPoll || this.SelectedActionType == VeloraActionType.EndPoll;
            }
        }

        public bool ShowTargetUser
        {
            get
            {
                switch (this.SelectedActionType)
                {
                    case VeloraActionType.ModUser:
                    case VeloraActionType.UnmodUser:
                    case VeloraActionType.VIPUser:
                    case VeloraActionType.UnVIPUser:
                    case VeloraActionType.BanUser:
                    case VeloraActionType.UnbanUser:
                    case VeloraActionType.TimeoutUser:
                    case VeloraActionType.UntimeoutUser:
                    case VeloraActionType.Shoutout:
                    case VeloraActionType.GrantChannelPoints:
                    case VeloraActionType.DeductChannelPoints:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool ShowAmount
        {
            get
            {
                return this.SelectedActionType == VeloraActionType.TimeoutUser
                    || this.SelectedActionType == VeloraActionType.GrantChannelPoints
                    || this.SelectedActionType == VeloraActionType.DeductChannelPoints || this.ShowPoll;
            }
        }

        public bool ShowReason
        {
            get { return this.SelectedActionType == VeloraActionType.BanUser || this.SelectedActionType == VeloraActionType.TimeoutUser; }
        }

        public bool ShowColor { get { return this.SelectedActionType == VeloraActionType.Announce; } }

        public bool ShowSendAsStreamer { get { return this.SelectedActionType == VeloraActionType.Announce; } }

        // Context-sensitive hints for the shared text / amount fields.
        public string TextHint
        {
            get
            {
                switch (this.SelectedActionType)
                {
                    case VeloraActionType.CreatePoll: return Resources.Question;
                    case VeloraActionType.EndPoll: return Resources.PollIdentifier;
                    case VeloraActionType.SetTitle: return MixItUp.Base.Resources.Title;
                    case VeloraActionType.SetGame: return MixItUp.Base.Resources.Game;
                    case VeloraActionType.Announce: return MixItUp.Base.Resources.Message;
                    case VeloraActionType.Raid: return MixItUp.Base.Resources.ChannelName;
                    default: return MixItUp.Base.Resources.Name;
                }
            }
        }

        public string AmountHint
        {
            get { return this.SelectedActionType == VeloraActionType.TimeoutUser || this.ShowPoll ? MixItUp.Base.Resources.DurationSeconds : MixItUp.Base.Resources.Amount; }
        }

        public VeloraActionEditorControlViewModel(VeloraActionModel action)
            : base(action)
        {
            this.SelectedActionType = action.ActionType;
            this.Text = action.Text;
            this.TargetUsername = action.TargetUsername;
            this.Amount = action.Amount;
            this.Reason = action.Reason;
            this.SelectedAnnounceColor = action.AnnounceColor;
            this.SendAsStreamer = action.SendAsStreamer;
            this.PollOptions = action.PollOptions;
            this.PollMultiple = action.PollMultiple;
        }

        public VeloraActionEditorControlViewModel() : base() { }

        public override async Task<Result> Validate()
        {
            if (this.ShowPoll)
            {
                int count = (this.PollOptions ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Count(o => !string.IsNullOrWhiteSpace(o));
                if (string.IsNullOrWhiteSpace(this.PollOptions) || (!SpecialIdentifierStringBuilder.ContainsSpecialIdentifiers(this.PollOptions) && (count < 2 || count > 6)) ||
                    (!SpecialIdentifierStringBuilder.ContainsSpecialIdentifiers(this.Amount) && (!int.TryParse(this.Amount, out int duration) || duration < 15 || duration > 3600)))
                {
                    return new Result(Resources.VeloraPollSettingsHint);
                }
            }
            if (this.ShowText && string.IsNullOrEmpty(this.Text))
            {
                return new Result(MixItUp.Base.Resources.ValidValueMustBeSpecified);
            }

            if ((this.SelectedActionType == VeloraActionType.GrantChannelPoints || this.SelectedActionType == VeloraActionType.DeductChannelPoints)
                && string.IsNullOrEmpty(this.Amount))
            {
                return new Result(MixItUp.Base.Resources.ValidValueMustBeSpecified);
            }

            return await base.Validate();
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            VeloraActionModel action = VeloraActionModel.CreateAction(this.SelectedActionType);
            action.Text = this.Text;
            action.TargetUsername = this.TargetUsername;
            action.Amount = this.Amount;
            action.Reason = this.Reason;
            action.AnnounceColor = this.SelectedAnnounceColor;
            action.SendAsStreamer = this.SendAsStreamer;
            action.PollOptions = this.PollOptions;
            action.PollMultiple = this.PollMultiple;
            return Task.FromResult<ActionModelBase>(action);
        }
    }
}
