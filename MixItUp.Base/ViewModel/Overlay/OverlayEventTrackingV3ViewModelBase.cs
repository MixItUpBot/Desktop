using Google.Apis.YouTube.v3.Data;
using MixItUp.Base.Model.Overlay;
using MixItUp.Base.Services;
using MixItUp.Base.Services.YouTube;
using MixItUp.Base.Services.YouTube.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System.Collections.ObjectModel;

namespace MixItUp.Base.ViewModel.Overlay
{
    public class OverlayEventTrackingYouTubeMembershipViewModel : UIViewModelBase
    {
        public string Name
        {
            get { return this.name; }
            set
            {
                this.name = value;
                this.NotifyPropertyChanged();
            }
        }
        private string name;

        public double Amount
        {
            get { return this.damageAmount; }
            set
            {
                this.damageAmount = value;
                this.NotifyPropertyChanged();
            }
        }
        private double damageAmount;

        public OverlayEventTrackingYouTubeMembershipViewModel(string name, double amount)
        {
            this.Name = name;
            this.Amount = amount;
        }
    }

    public abstract class OverlayEventTrackingV3ViewModelBase : OverlayVisualTextV3ViewModelBase
    {
        private const double SampleIntegerAmount = 123;
        private const double SampleDecimalAmount = 12.34;

        public abstract string EquationUnits { get; }

        public double FollowAmount
        {
            get { return this.followAmount; }
            set
            {
                this.followAmount = value;
                this.NotifyPropertyChanged();
            }
        }
        private double followAmount;

        public double RaidAmount
        {
            get { return this.raidAmount; }
            set
            {
                this.raidAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.RaidEquation));
            }
        }
        private double raidAmount;

        public double RaidPerViewAmount
        {
            get { return this.raidPerViewAmount; }
            set
            {
                this.raidPerViewAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.RaidEquation));
            }
        }
        private double raidPerViewAmount;

        public string RaidEquation
        {
            get
            {
                double total = this.RaidAmount + (this.RaidPerViewAmount * SampleIntegerAmount);
                return $"{this.RaidAmount} + ({this.RaidPerViewAmount} * {SampleIntegerAmount} {Resources.Viewers}) = {total} {this.EquationUnits}";
            }
        }

        public double TwitchSubscriptionTier1Amount
        {
            get { return this.twitchSubscriptionTier1Amount; }
            set
            {
                this.twitchSubscriptionTier1Amount = value;
                this.NotifyPropertyChanged();
            }
        }
        private double twitchSubscriptionTier1Amount;

        public double TwitchSubscriptionTier2Amount
        {
            get { return this.twitchSubscriptionTier2Amount; }
            set
            {
                this.twitchSubscriptionTier2Amount = value;
                this.NotifyPropertyChanged();
            }
        }
        private double twitchSubscriptionTier2Amount;

        public double TwitchSubscriptionTier3Amount
        {
            get { return this.twitchSubscriptionTier3Amount; }
            set
            {
                this.twitchSubscriptionTier3Amount = value;
                this.NotifyPropertyChanged();
            }
        }
        private double twitchSubscriptionTier3Amount;

        public double KickSubscriptionAmount
        {
            get { return this.kickSubscriptionAmount; }
            set
            {
                this.kickSubscriptionAmount = value;
                this.NotifyPropertyChanged();
            }
        }
        private double kickSubscriptionAmount;

        public double VeloraSubscriptionAmount
        {
            get { return this.veloraSubscriptionAmount; }
            set
            {
                this.veloraSubscriptionAmount = value;
                this.NotifyPropertyChanged();
            }
        }
        private double veloraSubscriptionAmount;

        public double VPZoneSubscriptionAmount
        {
            get { return this.vpzoneSubscriptionAmount; }
            set
            {
                this.vpzoneSubscriptionAmount = value;
                this.NotifyPropertyChanged();
            }
        }
        private double vpzoneSubscriptionAmount;

        public ObservableCollection<OverlayEventTrackingYouTubeMembershipViewModel> YouTubeMemberships { get; set; } = new ObservableCollection<OverlayEventTrackingYouTubeMembershipViewModel>();

        public double TwitchBitsAmount
        {
            get { return this.twitchBitsAmount; }
            set
            {
                this.twitchBitsAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.TwitchBitsEquation));
            }
        }
        private double twitchBitsAmount;

        public string TwitchBitsEquation
        {
            get
            {
                double total = this.TwitchBitsAmount * SampleIntegerAmount;
                return $"{this.TwitchBitsAmount} * {SampleIntegerAmount} {Resources.Bits} = {total} {this.EquationUnits}";
            }
        }

        public double YouTubeSuperChatAmount
        {
            get { return this.youTubeSuperChatAmount; }
            set
            {
                this.youTubeSuperChatAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.YouTubeSuperChatEquation));
            }
        }
        private double youTubeSuperChatAmount;

        public string YouTubeSuperChatEquation
        {
            get
            {
                double total = this.YouTubeSuperChatAmount * SampleDecimalAmount;
                return $"{this.YouTubeSuperChatAmount} * {CurrencyHelper.ToCurrencyString(SampleDecimalAmount)} = {total} {this.EquationUnits}";
            }
        }

        public double KickKicksAmount
        {
            get { return this.kickKicksAmount; }
            set
            {
                this.kickKicksAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.KickKicksEquation));
            }
        }
        private double kickKicksAmount;

        public string KickKicksEquation
        {
            get
            {
                double total = this.KickKicksAmount * SampleIntegerAmount;
                return $"{this.KickKicksAmount} * {SampleIntegerAmount} {Resources.KickKicks} = {total} {this.EquationUnits}";
            }
        }

        public double VeloraCheeredAmount
        {
            get { return this.veloraCheeredAmount; }
            set
            {
                this.veloraCheeredAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.VeloraCheeredEquation));
            }
        }
        private double veloraCheeredAmount;

        public double VPZoneCheeredAmount
        {
            get { return this.vpzoneCheeredAmount; }
            set
            {
                this.vpzoneCheeredAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.VPZoneCheeredEquation));
            }
        }
        public double JoystickTippedAmount
        {
            get { return this.joystickTippedAmount; }
            set
            {
                this.joystickTippedAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.JoystickTippedEquation));
            }
        }
        public double RazoGiftAmount
        {
            get { return this.razoGiftAmount; }
            set
            {
                this.razoGiftAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.RazoGiftEquation));
            }
        }
        public double RazoSubscriptionAmount
        {
            get { return this.razoSubscriptionAmount; }
            set
            {
                this.razoSubscriptionAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.RazoSubscriptionEquation));
            }
        }
        public double JoystickSubscriptionAmount
        {
            get { return this.joystickSubscriptionAmount; }
            set
            {
                this.joystickSubscriptionAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.JoystickSubscriptionEquation));
            }
        }
        private double vpzoneCheeredAmount;
        private double joystickTippedAmount;
        private double razoGiftAmount;
        private double joystickSubscriptionAmount;
        private double razoSubscriptionAmount;

        public string VeloraCheeredEquation
        {
            get
            {
                double total = this.VeloraCheeredAmount * SampleIntegerAmount;
                return $"{this.VeloraCheeredAmount} * {SampleIntegerAmount} {Resources.VeloraCheered} = {total} {this.EquationUnits}";
            }
        }

        public string VPZoneCheeredEquation
        {
            get
            {
                double total = this.VPZoneCheeredAmount * SampleIntegerAmount;
                return $"{this.VPZoneCheeredAmount} * {SampleIntegerAmount} {Resources.VPZoneCheered} = {total} {this.EquationUnits}";
            }
        }
        public string JoystickTippedEquation
        {
            get
            {
                double total = this.JoystickTippedAmount * SampleIntegerAmount;
                return $"{this.JoystickTippedAmount} * {SampleIntegerAmount} {Resources.JoystickTipped} = {total} {this.EquationUnits}";
            }
        }
        public string RazoGiftEquation
        {
            get
            {
                double total = this.RazoGiftAmount * SampleIntegerAmount;
                return $"{this.RazoGiftAmount} * {SampleIntegerAmount} {Resources.RazoGift} = {total} {this.EquationUnits}";
            }
        }
        public string RazoSubscriptionEquation
        {
            get
            {
                double total = this.RazoSubscriptionAmount * SampleIntegerAmount;
                return $"{this.RazoSubscriptionAmount} * {SampleIntegerAmount} {Resources.RazoSubscriptions} = {total} {this.EquationUnits}";
            }
        }
        public string JoystickSubscriptionEquation
        {
            get
            {
                double total = this.JoystickSubscriptionAmount * SampleIntegerAmount;
                return $"{this.JoystickSubscriptionAmount} * {SampleIntegerAmount} {Resources.JoystickSubscriptions} = {total} {this.EquationUnits}";
            }
        }

        public double DonationAmount
        {
            get { return this.donationAmount; }
            set
            {
                this.donationAmount = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(DonationEquation));
            }
        }
        private double donationAmount;

        public string DonationEquation
        {
            get
            {
                double total = this.DonationAmount * SampleDecimalAmount;
                return $"{this.DonationAmount} * {CurrencyHelper.ToCurrencyString(SampleDecimalAmount)} = {total} {this.EquationUnits}";
            }
        }

        public OverlayEventTrackingV3ViewModelBase(OverlayItemV3Type type)
            : base(type)
        {
            if (ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                foreach (MembershipsLevel membershipsLevel in ServiceManager.Get<YouTubeSession>().MembershipLevels)
                {
                    this.YouTubeMemberships.Add(new OverlayEventTrackingYouTubeMembershipViewModel(membershipsLevel.Snippet.LevelDetails.DisplayName, 0));
                }
            }

            this.InitializeInternal();
        }

        public OverlayEventTrackingV3ViewModelBase(OverlayEventCountingV3ModelBase item)
            : base(item)
        {
            this.FollowAmount = item.FollowAmount;

            this.RaidAmount = item.RaidAmount;
            this.RaidPerViewAmount = item.RaidPerViewAmount;

            this.TwitchSubscriptionTier1Amount = item.TwitchSubscriptionsAmount[1];
            this.TwitchSubscriptionTier2Amount = item.TwitchSubscriptionsAmount[2];
            this.TwitchSubscriptionTier3Amount = item.TwitchSubscriptionsAmount[3];
            this.TwitchBitsAmount = item.TwitchBitsAmount;

            item.KickSubscriptionsAmount.TryGetValue(1, out double kickTier1);
            this.KickSubscriptionAmount = kickTier1;

            item.VeloraSubscriptionsAmount.TryGetValue(1, out double veloraTier1);
            this.VeloraSubscriptionAmount = veloraTier1;

            item.VPZoneSubscriptionsAmount.TryGetValue(1, out double vpzoneTier1);
            this.VPZoneSubscriptionAmount = vpzoneTier1;

            if (ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                foreach (MembershipsLevel membershipsLevel in ServiceManager.Get<YouTubeSession>().MembershipLevels)
                {
                    if (item.YouTubeMembershipsAmount.TryGetValue(membershipsLevel.Snippet.LevelDetails.DisplayName, out double damageAmount))
                    {
                        this.YouTubeMemberships.Add(new OverlayEventTrackingYouTubeMembershipViewModel(membershipsLevel.Snippet.LevelDetails.DisplayName, damageAmount));
                    }
                    else
                    {
                        this.YouTubeMemberships.Add(new OverlayEventTrackingYouTubeMembershipViewModel(membershipsLevel.Snippet.LevelDetails.DisplayName, 0));
                    }
                }
            }
            this.YouTubeSuperChatAmount = item.YouTubeSuperChatAmount;

            this.KickKicksAmount = item.KickKicksAmount;

            this.VeloraCheeredAmount = item.VeloraCheeredAmount;
            this.VPZoneCheeredAmount = item.VPZoneCheeredAmount;
            this.JoystickTippedAmount = item.JoystickTippedAmount;
            this.RazoGiftAmount = item.RazoGiftAmount;
            this.RazoSubscriptionAmount = item.RazoSubscriptionAmount;
            this.JoystickSubscriptionAmount = item.JoystickSubscriptionAmount;

            this.DonationAmount = item.DonationAmount;

            this.InitializeInternal();
        }

        public override Result Validate()
        {
            return new Result();
        }

        protected void AssignProperties(OverlayEventCountingV3ModelBase result)
        {
            base.AssignProperties(result);

            result.FollowAmount = this.FollowAmount;

            result.RaidAmount = this.RaidAmount;
            result.RaidPerViewAmount = this.RaidPerViewAmount;

            result.TwitchSubscriptionsAmount[1] = this.TwitchSubscriptionTier1Amount;
            result.TwitchSubscriptionsAmount[2] = this.TwitchSubscriptionTier2Amount;
            result.TwitchSubscriptionsAmount[3] = this.TwitchSubscriptionTier3Amount;
            result.TwitchBitsAmount = this.TwitchBitsAmount;

            result.KickSubscriptionsAmount[1] = this.KickSubscriptionAmount;

            result.VeloraSubscriptionsAmount[1] = this.VeloraSubscriptionAmount;

            result.VPZoneSubscriptionsAmount[1] = this.VPZoneSubscriptionAmount;

            result.YouTubeMembershipsAmount.Clear();
            foreach (OverlayEventTrackingYouTubeMembershipViewModel membership in this.YouTubeMemberships)
            {
                result.YouTubeMembershipsAmount[membership.Name] = membership.Amount;
            }
            result.YouTubeSuperChatAmount = this.YouTubeSuperChatAmount;

            result.KickKicksAmount = this.KickKicksAmount;

            result.VeloraCheeredAmount = this.VeloraCheeredAmount;

            result.VPZoneCheeredAmount = this.VPZoneCheeredAmount;
            result.JoystickTippedAmount = this.JoystickTippedAmount;
            result.RazoGiftAmount = this.RazoGiftAmount;
            result.RazoSubscriptionAmount = this.RazoSubscriptionAmount;
            result.JoystickSubscriptionAmount = this.JoystickSubscriptionAmount;

            result.DonationAmount = this.DonationAmount;
        }

        private void InitializeInternal()
        {
            foreach (OverlayEventTrackingYouTubeMembershipViewModel membership in this.YouTubeMemberships)
            {
                membership.PropertyChanged += (sender, e) =>
                {
                    this.NotifyPropertyChanged("X");
                };
            }
        }
    }
}
