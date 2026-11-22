using MixItUp.Base.Model.Overlay;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Overlay
{
    public class OverlayLabelDisplayV3ViewModel : UIViewModelBase
    {
        public OverlayLabelDisplayV3TypeEnum Type { get; private set; }

        public string TypeString { get { return Resources.ResourceManager.GetSafeString(this.Type.ToString()); } }

        public bool IsCounterType { get { return this.Type == OverlayLabelDisplayV3TypeEnum.Counter; } }

        public bool IsFileType { get { return this.Type == OverlayLabelDisplayV3TypeEnum.File; } }

        public bool IsDateTimeType { get { return OverlayLabelV3Model.IsDateTimeDisplay(this.Type); } }

        public bool ShowFormat { get { return !this.IsFileType; } }

        public string FormatToolTip { get { return this.IsDateTimeType ? Resources.OverlayLabelDateTimeFormatTooltip + Environment.NewLine + Environment.NewLine + Resources.OverlayLabelDateTimeStylingTooltip : null; } }

        public int GridWidth { get { return (this.IsFileType || this.IsDateTimeType) ? 620 : 300; } }

        public bool IsEnabled
        {
            get { return this.isEnabled; }
            set
            {
                this.isEnabled = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isEnabled;

        public string Format
        {
            get { return this.format; }
            set
            {
                this.format = value;
                this.NotifyPropertyChanged();
            }
        }
        private string format;

        public ObservableCollection<CounterModel> Counters { get; set; } = new ObservableCollection<CounterModel>();

        public CounterModel SelectedCounter
        {
            get { return this.selectedCounter; }
            set
            {
                this.selectedCounter = value;
                this.NotifyPropertyChanged();
            }
        }
        private CounterModel selectedCounter;

        public string FilePath
        {
            get { return this.filePath; }
            set
            {
                this.filePath = value;
                this.NotifyPropertyChanged();
            }
        }
        private string filePath;
        public ICommand BrowseFilePathCommand { get; private set; }

        public ObservableCollection<string> TimeZones { get; set; } = new ObservableCollection<string>();

        public string TimeZoneID
        {
            get { return this.timeZoneID; }
            set
            {
                this.timeZoneID = value;
                this.NotifyPropertyChanged();
            }
        }
        private string timeZoneID;

        public OverlayLabelDisplayV3Model Model { get; private set; }

        public OverlayLabelDisplayV3ViewModel(OverlayLabelDisplayV3TypeEnum type)
        {
            this.Type = type;
            switch (this.Type)
            {
                case OverlayLabelDisplayV3TypeEnum.ViewerCount:
                case OverlayLabelDisplayV3TypeEnum.ChatterCount:
                case OverlayLabelDisplayV3TypeEnum.Counter:
                case OverlayLabelDisplayV3TypeEnum.TotalFollowers:
                case OverlayLabelDisplayV3TypeEnum.TotalSubscribers:
                    this.Format = OverlayLabelV3ViewModel.AmountItemTemplate;
                    break;

                case OverlayLabelDisplayV3TypeEnum.LatestFollower:
                case OverlayLabelDisplayV3TypeEnum.LatestSubscriber:
                    this.Format = OverlayLabelV3ViewModel.UsernameItemTemplate;
                    break;

                case OverlayLabelDisplayV3TypeEnum.LatestRaid:
                case OverlayLabelDisplayV3TypeEnum.LatestDonation:
                case OverlayLabelDisplayV3TypeEnum.LatestTwitchBits:
                case OverlayLabelDisplayV3TypeEnum.LatestYouTubeSuperChat:
                case OverlayLabelDisplayV3TypeEnum.LatestKicksGifted:
                case OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter:
                case OverlayLabelDisplayV3TypeEnum.LatestVeloraCheered:
                case OverlayLabelDisplayV3TypeEnum.LatestVPZoneCheered:
                case OverlayLabelDisplayV3TypeEnum.LatestJoystickTipped:
                case OverlayLabelDisplayV3TypeEnum.LatestRazoGift:
                    this.Format = OverlayLabelV3ViewModel.UsernameAmountItemTemplate;
                    break;

                case OverlayLabelDisplayV3TypeEnum.Date:
                    this.Format = OverlayLabelV3ViewModel.DateItemTemplate;
                    break;

                case OverlayLabelDisplayV3TypeEnum.Time:
                    this.Format = OverlayLabelV3ViewModel.TimeItemTemplate;
                    break;
            }

            this.Initialize();
        }

        public OverlayLabelDisplayV3ViewModel(OverlayLabelDisplayV3Model model)
        {
            this.Model = model;

            this.Type = model.Type;
            this.IsEnabled = model.IsEnabled;
            this.Format = model.Format;
            this.FilePath = model.FilePath;
            this.TimeZoneID = model.TimeZoneID;

            this.Initialize();
            if (this.Type == OverlayLabelDisplayV3TypeEnum.Counter && !string.IsNullOrEmpty(this.Model.CounterName))
            {
                this.SelectedCounter = this.Counters.FirstOrDefault(c => string.Equals(c.Name, this.Model.CounterName, StringComparison.OrdinalIgnoreCase));
            }
        }

        // The overlay renders date and time in the browser, which expects IANA IDs
        // such as "Europe/Berlin" rather than the Windows IDs .NET reports. Windows
        // only maps to the primary city of each zone, so the combo box stays editable
        // for anyone who wants a specific IANA name the conversion does not produce.
        private static IEnumerable<string> GetTimeZoneIDs()
        {
            if (OverlayLabelDisplayV3ViewModel.timeZoneIDs == null)
            {
                SortedSet<string> results = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (TimeZoneInfo timeZone in TimeZoneInfo.GetSystemTimeZones())
                    {
                        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out string ianaID))
                        {
                            results.Add(ianaID);
                        }
                        else if (timeZone.Id.Contains("/"))
                        {
                            results.Add(timeZone.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                OverlayLabelDisplayV3ViewModel.timeZoneIDs = results;
            }
            return OverlayLabelDisplayV3ViewModel.timeZoneIDs;
        }
        private static IEnumerable<string> timeZoneIDs;

        public Result Validate()
        {
            if (!this.IsEnabled)
            {
                return new Result();
            }

            if (this.ShowFormat && string.IsNullOrEmpty(this.Format))
            {
                return new Result(Resources.OverlayLabelDisplayFormatMustHaveValidValue);
            }

            if (this.Type == OverlayLabelDisplayV3TypeEnum.Counter && this.SelectedCounter == null)
            {
                return new Result(Resources.OverlayLabelCounterNotSelected);
            }

            if (this.Type == OverlayLabelDisplayV3TypeEnum.File && string.IsNullOrEmpty(this.FilePath))
            {
                return new Result(Resources.OverlayLabelFilePathMustBeSpecified);
            }

            return new Result();
        }

        private void Initialize()
        {
            if (this.Type == OverlayLabelDisplayV3TypeEnum.Counter)
            {
                foreach (var counter in ChannelSession.Settings.Counters)
                {
                    this.Counters.Add(counter.Value);
                }
            }

            if (this.IsDateTimeType)
            {
                foreach (string timeZone in OverlayLabelDisplayV3ViewModel.GetTimeZoneIDs())
                {
                    this.TimeZones.Add(timeZone);
                }
            }

            this.BrowseFilePathCommand = this.CreateCommand(async () =>
            {
                string filepath = ServiceManager.Get<IFileService>().ShowOpenFileDialog(ServiceManager.Get<IFileService>().TextFileFilter());
                if (!string.IsNullOrWhiteSpace(filepath))
                {
                    this.FilePath = filepath;
                    if (ServiceManager.Get<IFileService>().FileExists(filePath))
                    {
                        this.Format = await ServiceManager.Get<IFileService>().ReadFile(filepath);
                    }
                }
            });
        }
    }

    public class OverlayLabelV3ViewModel : OverlayVisualTextV3ViewModelBase
    {
        public static readonly string UsernameItemTemplate = $"{{{OverlayLabelV3Model.UsernamePropertyName}}}";
        public static readonly string AmountItemTemplate = $"{{{OverlayLabelV3Model.AmountPropertyName}}}";
        public static readonly string UsernameAmountItemTemplate = $"{{{OverlayLabelV3Model.UsernamePropertyName}}} - {{{OverlayLabelV3Model.AmountPropertyName}}}";
        public static readonly string DateItemTemplate = "dddd, MMMM D";
        public static readonly string TimeItemTemplate = "h:mm A";

        public override string DefaultHTML { get { return OverlayLabelV3Model.DefaultHTML; } }
        public override string DefaultCSS { get { return OverlayLabelV3Model.DefaultCSS; } }
        public override string DefaultJavascript { get { return OverlayLabelV3Model.DefaultJavascript; } }

        public IEnumerable<OverlayLabelDisplayV3SettingTypeEnum> DisplaySettings { get; private set; } = EnumHelper.GetEnumList<OverlayLabelDisplayV3SettingTypeEnum>();

        public OverlayLabelDisplayV3SettingTypeEnum SelectedDisplaySetting
        {
            get { return this.selectedDisplaySetting; }
            set
            {
                this.selectedDisplaySetting = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.IsRotationDisplaySetting));
            }
        }
        private OverlayLabelDisplayV3SettingTypeEnum selectedDisplaySetting;

        public bool IsRotationDisplaySetting { get { return this.SelectedDisplaySetting == OverlayLabelDisplayV3SettingTypeEnum.RotatingDisplays; } }

        public int DisplayRotationSeconds
        {
            get { return this.displayRotationSeconds; }
            set
            {
                this.displayRotationSeconds = value;
                this.NotifyPropertyChanged();
            }
        }
        private int displayRotationSeconds = 5;

        public ObservableCollection<OverlayLabelDisplayV3ViewModel> Displays { get; set; } = new ObservableCollection<OverlayLabelDisplayV3ViewModel>();

        public OverlayAnimationV3ViewModel DisplayEntranceAnimation;
        public OverlayAnimationV3ViewModel DisplayExitAnimation;

        public OverlayLabelV3ViewModel()
            : base(OverlayItemV3Type.Label)
        {
            this.DisplayEntranceAnimation = new OverlayAnimationV3ViewModel(Resources.Entrance, new OverlayAnimationV3Model());
            this.DisplayExitAnimation = new OverlayAnimationV3ViewModel(Resources.Exit, new OverlayAnimationV3Model());

            this.Initialize();

            this.Displays.First(d => d.Type == OverlayLabelDisplayV3TypeEnum.LatestSubscriber).IsEnabled = true;
            this.Displays.First(d => d.Type == OverlayLabelDisplayV3TypeEnum.LatestRaid).IsEnabled = true;
        }

        public OverlayLabelV3ViewModel(OverlayLabelV3Model item)
            : base(item)
        {
            this.SelectedDisplaySetting = item.DisplaySetting;
            this.DisplayRotationSeconds = item.DisplayRotationSeconds;

            foreach (var display in item.Displays)
            {
                this.Displays.Add(new OverlayLabelDisplayV3ViewModel(display.Value));
            }

            this.DisplayEntranceAnimation = new OverlayAnimationV3ViewModel(Resources.Entrance, item.DisplayEntranceAnimation);
            this.DisplayExitAnimation = new OverlayAnimationV3ViewModel(Resources.Exit, item.DisplayExitAnimation);

            this.Initialize();
        }

        public override Result Validate()
        {
            if (this.DisplayRotationSeconds <= 0)
            {
                return new Result(Resources.OverlayLabelErrorDisplayRotationMustBePositiveNumber);
            }

            if (this.Displays.All(d => !d.IsEnabled))
            {
                return new Result(Resources.OverlayLabelErrorAtLeastOneDisplayTypeMustBeEnabled);
            }

            foreach (var display in this.Displays)
            {
                Result result = display.Validate();
                if (!result.Success)
                {
                    return result;
                }
            }

            return new Result();
        }

        protected override OverlayItemV3ModelBase GetItemInternal()
        {
            OverlayLabelV3Model result = new OverlayLabelV3Model();

            this.AssignProperties(result);

            result.DisplaySetting = this.SelectedDisplaySetting;
            result.DisplayRotationSeconds = this.DisplayRotationSeconds;

            result.DisplayEntranceAnimation = this.DisplayEntranceAnimation.GetAnimation();
            result.DisplayExitAnimation = this.DisplayExitAnimation.GetAnimation();

            foreach (OverlayLabelDisplayV3ViewModel display in this.Displays)
            {
                result.Displays[display.Type] = new OverlayLabelDisplayV3Model()
                {
                    Type = display.Type,
                    IsEnabled = display.IsEnabled,
                    Format = display.Format,

                    UserID = display.Model?.UserID ?? Guid.Empty,
                    Amount = display.Model?.Amount ?? 0,

                    CounterName = display.SelectedCounter?.Name ?? null,

                    FilePath = display.FilePath,

                    TimeZoneID = display.TimeZoneID,
                };
            }

            return result;
        }

        private void Initialize()
        {
            this.Animations.Add(this.DisplayEntranceAnimation);
            this.Animations.Add(this.DisplayExitAnimation);

            foreach (OverlayLabelDisplayV3TypeEnum labelType in EnumHelper.GetEnumList<OverlayLabelDisplayV3TypeEnum>())
            {
                if (!this.Displays.Any(d => d.Type == labelType))
                {
                    this.Displays.Add(new OverlayLabelDisplayV3ViewModel(labelType));
                }
            }

            foreach (OverlayLabelDisplayV3ViewModel display in this.Displays)
            {
                display.PropertyChanged += (sender, e) =>
                {
                    this.NotifyPropertyChanged("X");
                };
            }
        }
    }
}
