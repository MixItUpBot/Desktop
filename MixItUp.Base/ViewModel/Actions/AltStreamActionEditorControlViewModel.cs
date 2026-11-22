using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class AltStreamActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type => ActionTypeEnum.AltStream;
        private readonly AltStreamActionType? savedActionType;
        public IEnumerable<AltStreamActionType> ActionTypes => EnumHelper.GetEnumList<AltStreamActionType>(includeObsoletes: true)
            .Where(type => !EnumHelper.IsObsolete(type) || type == this.savedActionType);
        public string TargetHint => this.SelectedActionType is AltStreamActionType.FulfillRedemption or AltStreamActionType.RefundRedemption ? Resources.RedemptionIdentifier : Resources.Username;

        private AltStreamActionType selectedActionType;
        public AltStreamActionType SelectedActionType
        {
            get => this.selectedActionType;
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.TargetHint));
            }
        }

        public string Target { get; set; }

        public AltStreamActionEditorControlViewModel() { }

        public AltStreamActionEditorControlViewModel(AltStreamActionModel action) : base(action)
        {
            this.savedActionType = action.ActionType;
            this.SelectedActionType = action.ActionType;
            this.Target = action.Target;
        }

        public override Task<Result> Validate()
        {
            if (string.IsNullOrWhiteSpace(this.Target) && EnumHelper.GetEnumList<AltStreamActionType>().Contains(this.SelectedActionType))
            {
                return Task.FromResult(new Result(Resources.ValidValueMustBeSpecified));
            }
            return base.Validate();
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            return Task.FromResult<ActionModelBase>(new AltStreamActionModel(this.SelectedActionType, this.Target));
        }
    }
}
