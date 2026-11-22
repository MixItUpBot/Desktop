using MixItUp.Base.Model.Commands;

using MixItUp.Base.Services;
using MixItUp.Base.Services.AltStream;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Commands
{
    public class AltStreamChannelPointsCommandEditorWindowViewModel : CommandEditorWindowViewModelBase
    {
        public ObservableCollection<AltStreamSession.Reward> ChannelPointRewards { get; set; } = new ObservableCollection<AltStreamSession.Reward>();

        public AltStreamSession.Reward ChannelPointReward
        {
            get { return this.channelPointReward; }
            set
            {
                this.channelPointReward = value;
                this.NotifyPropertyChanged();

                this.Name = (this.channelPointReward != null) ? this.channelPointReward.Name : string.Empty;
            }
        }
        private AltStreamSession.Reward channelPointReward;

        private string existingChannelPointRewardID = string.Empty;

        public AltStreamChannelPointsCommandEditorWindowViewModel(AltStreamChannelPointsCommandModel existingCommand)
            : base(existingCommand)
        {
            this.existingChannelPointRewardID = existingCommand.ChannelPointRewardID;
        }

        public AltStreamChannelPointsCommandEditorWindowViewModel() : base(CommandTypeEnum.AltStreamChannelPoints) { }

        public override Task<Result> Validate()
        {
            if (this.ChannelPointReward == null)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ChannelPointRewardMissing));
            }

            return Task.FromResult(new Result());
        }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return AltStreamChannelPointsCommandModel.GetChannelPointTestSpecialIdentifiers(); }

        public override Task<CommandModelBase> CreateNewCommand() { return Task.FromResult<CommandModelBase>(new AltStreamChannelPointsCommandModel(this.ChannelPointReward.Name, this.ChannelPointReward.ID)); }

        public override async Task UpdateExistingCommand(CommandModelBase command)
        {
            await base.UpdateExistingCommand(command);
            ((AltStreamChannelPointsCommandModel)command).ChannelPointRewardID = this.ChannelPointReward.ID;
        }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            ServiceManager.Get<CommandService>().AltStreamChannelPointsCommands.Remove((AltStreamChannelPointsCommandModel)this.existingCommand);
            ServiceManager.Get<CommandService>().AltStreamChannelPointsCommands.Add((AltStreamChannelPointsCommandModel)command);
            return Task.CompletedTask;
        }

        protected override async Task OnOpenInternal()
        {
            if (ServiceManager.Get<AltStreamSession>().IsConnected)
            {
                IEnumerable<AltStreamSession.Reward> rewards = await ServiceManager.Get<AltStreamSession>().GetRewards();
                if (rewards != null && rewards.Count() > 0)
                {
                    foreach (AltStreamSession.Reward channelPoint in rewards.OrderBy(c => c.Name))
                    {
                        this.ChannelPointRewards.Add(channelPoint);
                    }

                    if (!string.IsNullOrEmpty(this.existingChannelPointRewardID))
                    {
                        this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => string.Equals(c.ID, this.existingChannelPointRewardID));
                    }
                    else if (!string.IsNullOrEmpty(this.Name))
                    {
                        this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => string.Equals(c.Name, this.Name));
                    }
                }
            }
            await base.OnOpenInternal();
        }
    }
}
