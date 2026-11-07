using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Actions
{
    public class ActionEditorListControlViewModel : UIViewModelBase
    {
        public ObservableCollection<ActionTypeEnum> ActionTypes { get; set; } = new ObservableCollection<ActionTypeEnum>();
        public ActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
            }
        }
        private ActionTypeEnum selectedActionType;

        public ICommand AddCommand { get; private set; }

        public BulkObservableCollection<ActionEditorControlViewModelBase> Actions { get; set; } = new BulkObservableCollection<ActionEditorControlViewModelBase>();

        public ActionEditorControlViewModelBase ParentAction { get; set; }

        public ActionEditorListControlViewModel()
        {
            List<ActionTypeEnum> actionTypes = new List<ActionTypeEnum>(EnumHelper.GetEnumList<ActionTypeEnum>());
            actionTypes.Remove(ActionTypeEnum.Custom);
            actionTypes.RemoveAll(action => ChannelSession.Settings.PlatformsToHide.Any(platform => platform.ToString() == action.ToString()));
            foreach (ActionTypeEnum hiddenActions in ChannelSession.Settings.ActionsToHide)
            {
                actionTypes.Remove(hiddenActions);
            }

            this.ActionTypes.AddRange(actionTypes.OrderBy(a => a.ToString()));

            this.AddCommand = this.CreateCommand(async () =>
            {
                if (this.ActionTypes.Contains(this.SelectedActionType))
                {
                    ActionEditorControlViewModelBase editorViewModel = null;
                    switch (this.SelectedActionType)
                    {
                        case ActionTypeEnum.Chat: editorViewModel = new ChatActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Command: editorViewModel = new CommandActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Conditional: editorViewModel = new ConditionalActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Consumables: editorViewModel = new ConsumablesActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Counter: editorViewModel = new CounterActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Discord: editorViewModel = new DiscordActionEditorControlViewModel(); break;
                        case ActionTypeEnum.ExternalProgram: editorViewModel = new ExternalProgramActionEditorControlViewModel(); break;
                        case ActionTypeEnum.File: editorViewModel = new FileActionEditorControlViewModel(); break;
                        case ActionTypeEnum.GameQueue: editorViewModel = new GameQueueActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Group: editorViewModel = new GroupActionEditorControlViewModel(); break;
                        case ActionTypeEnum.IFTTT: editorViewModel = new IFTTTActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Input: editorViewModel = new InputActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Kick: editorViewModel = new KickActionEditorControlViewModel(); break;
                        case ActionTypeEnum.LumiaStream: editorViewModel = new LumiaStreamActionEditorControlViewModel(); break;
                        case ActionTypeEnum.MeldStudio: editorViewModel = new MeldStudioActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Moderation: editorViewModel = new ModerationActionEditorControlViewModel(); break;
                        case ActionTypeEnum.MtionStudio: editorViewModel = new MtionStudioActionViewModel(); break;
                        case ActionTypeEnum.MusicPlayer: editorViewModel = new MusicPlayerActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Overlay: editorViewModel = new OverlayActionEditorControlViewModel(); break;
                        case ActionTypeEnum.PlatformMessage: editorViewModel = new PlatformMessageActionEditorControlViewModel(); break;
                        case ActionTypeEnum.PixelChat: editorViewModel = new PixelChatActionEditorControlViewModel(); break;
                        case ActionTypeEnum.PolyPop: editorViewModel = new PolyPopActionEditorControlViewModel(); break;
                        case ActionTypeEnum.RahiTuber: editorViewModel = new RahiTuberActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Random: editorViewModel = new RandomActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Repeat: editorViewModel = new RepeatActionEditorControlViewModel(); break;
                        case ActionTypeEnum.SAMMI: editorViewModel = new SAMMIActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Script: editorViewModel = new ScriptActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Serial: editorViewModel = new SerialActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Sound: editorViewModel = new SoundActionEditorControlViewModel(); break;
                        case ActionTypeEnum.SpecialIdentifier: editorViewModel = new SpecialIdentifierActionEditorControlViewModel(); break;
                        case ActionTypeEnum.StreamingSoftware: editorViewModel = new StreamingSoftwareActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Streamlabs: editorViewModel = new StreamlabsActionEditorControlViewModel(); break;
                        case ActionTypeEnum.TextToSpeech: editorViewModel = new TextToSpeechActionEditorControlViewModel(); break;
                        case ActionTypeEnum.TITS: editorViewModel = new TITSActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Twitch: editorViewModel = new TwitchActionEditorControlViewModel(); break;
                        case ActionTypeEnum.UserLookup: editorViewModel = new UserLookupActionEditorControlViewModel(); break;
                        case ActionTypeEnum.VConnect: editorViewModel = new VConnectActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Veadotube: editorViewModel = new VeadotubeActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Velora: editorViewModel = new VeloraActionEditorControlViewModel(); break;
                        case ActionTypeEnum.VPZone: editorViewModel = new VPZoneActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Voicemod: editorViewModel = new VoicemodActionEditorControlViewModel(); break;
                        case ActionTypeEnum.VTSPog: editorViewModel = new VTSPogActionEditorControlViewModel(); break;
                        case ActionTypeEnum.VTubeStudio: editorViewModel = new VTubeStudioActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Wait: editorViewModel = new WaitActionEditorControlViewModel(); break;
                        case ActionTypeEnum.WebRequest: editorViewModel = new WebRequestActionEditorControlViewModel(); break;
                        case ActionTypeEnum.YouTube: editorViewModel = new YouTubeActionEditorControlViewModel(); break;
                    }

                    if (editorViewModel != null)
                    {
                        await this.AddActionViewModel(editorViewModel);
                    }
                }
            });
        }

        public virtual Dictionary<string, string> GetTestSpecialIdentifiers() { return CommandModelBase.GetGeneralTestSpecialIdentifiers(); }

        public void MoveActionUp(ActionEditorControlViewModelBase actionViewModel)
        {
            int index = this.Actions.IndexOf(actionViewModel);
            if (index > 0)
            {
                this.Actions.Remove(actionViewModel);
                this.Actions.Insert(index - 1, actionViewModel);
            }
        }

        public void MoveActionDown(ActionEditorControlViewModelBase actionViewModel)
        {
            int index = this.Actions.IndexOf(actionViewModel);
            if (index >= 0 && index < this.Actions.Count - 1)
            {
                this.Actions.Remove(actionViewModel);
                this.Actions.Insert(index + 1, actionViewModel);
            }
        }

        public async Task DuplicateAction(ActionEditorControlViewModelBase actionViewModel)
        {
            ActionModelBase action = await actionViewModel.ValidateAndGetAction();
            if (action != null)
            {
                await this.AddAction(action);
            }
        }

        public void DeleteAction(ActionEditorControlViewModelBase actionViewModel)
        {
            this.Actions.Remove(actionViewModel);
        }

        public bool CanDropAction(ActionEditorControlViewModelBase actionViewModel)
        {
            if (actionViewModel == null)
            {
                return false;
            }

            return !this.IsNestedUnderAction(actionViewModel);
        }

        public void DropAction(ActionEditorControlViewModelBase actionViewModel, int insertIndex)
        {
            if (!this.CanDropAction(actionViewModel))
            {
                return;
            }

            ActionEditorListControlViewModel sourceActionEditorListControlViewModel = actionViewModel.ActionEditorListControlViewModel;
            if (sourceActionEditorListControlViewModel == this)
            {
                int sourceIndex = this.Actions.IndexOf(actionViewModel);
                if (sourceIndex < 0)
                {
                    return;
                }

                insertIndex = this.GetSafeInsertIndex(insertIndex);
                if (sourceIndex < insertIndex)
                {
                    insertIndex--;
                }

                if (sourceIndex != insertIndex)
                {
                    this.Actions.RemoveAt(sourceIndex);
                    this.Actions.Insert(insertIndex, actionViewModel);
                }
                return;
            }

            if (sourceActionEditorListControlViewModel == null)
            {
                return;
            }

            sourceActionEditorListControlViewModel.Actions.Remove(actionViewModel);

            insertIndex = this.GetSafeInsertIndex(insertIndex);
            actionViewModel.Initialize(this);
            this.Actions.Insert(insertIndex, actionViewModel);
        }

        public async Task AddAction(ActionModelBase action)
        {
            ActionEditorControlViewModelBase editorViewModel = this.CreateEditorViewModel(action);
            if (editorViewModel != null)
            {
                await this.AddActionViewModel(editorViewModel);
            }
        }

        public async Task AddActions(IEnumerable<ActionModelBase> actions)
        {
            if (actions == null)
            {
                return;
            }

            List<ActionEditorControlViewModelBase> batch = new List<ActionEditorControlViewModelBase>();
            foreach (ActionModelBase action in actions)
            {
                ActionEditorControlViewModelBase editorViewModel = this.CreateEditorViewModel(action);
                if (editorViewModel != null)
                {
                    editorViewModel.Initialize(this);
                    batch.Add(editorViewModel);
                }

                if (batch.Count >= 20)
                {
                    this.Actions.AddRange(batch);
                    batch.Clear();
                    await Task.Yield();
                }
            }

            if (batch.Count > 0)
            {
                this.Actions.AddRange(batch);
            }
        }

        private ActionEditorControlViewModelBase CreateEditorViewModel(ActionModelBase action)
        {
            switch (action.Type)
            {
                case ActionTypeEnum.Chat: return new ChatActionEditorControlViewModel((ChatActionModel)action);
                case ActionTypeEnum.Command: return new CommandActionEditorControlViewModel((CommandActionModel)action);
                case ActionTypeEnum.Conditional: return new ConditionalActionEditorControlViewModel((ConditionalActionModel)action);
                case ActionTypeEnum.Consumables: return new ConsumablesActionEditorControlViewModel((ConsumablesActionModel)action);
                case ActionTypeEnum.Counter: return new CounterActionEditorControlViewModel((CounterActionModel)action);
                case ActionTypeEnum.Discord: return new DiscordActionEditorControlViewModel((DiscordActionModel)action);
                case ActionTypeEnum.ExternalProgram: return new ExternalProgramActionEditorControlViewModel((ExternalProgramActionModel)action);
                case ActionTypeEnum.File: return new FileActionEditorControlViewModel((FileActionModel)action);
                case ActionTypeEnum.GameQueue: return new GameQueueActionEditorControlViewModel((GameQueueActionModel)action);
                case ActionTypeEnum.Group: return new GroupActionEditorControlViewModel((GroupActionModel)action);
                case ActionTypeEnum.IFTTT: return new IFTTTActionEditorControlViewModel((IFTTTActionModel)action);
                case ActionTypeEnum.Input: return new InputActionEditorControlViewModel((InputActionModel)action);
                case ActionTypeEnum.Kick: return new KickActionEditorControlViewModel((KickActionModel)action);
                case ActionTypeEnum.LumiaStream: return new LumiaStreamActionEditorControlViewModel((LumiaStreamActionModel)action);
                case ActionTypeEnum.MeldStudio: return new MeldStudioActionEditorControlViewModel((MeldStudioActionModel)action);
                case ActionTypeEnum.Moderation: return new ModerationActionEditorControlViewModel((ModerationActionModel)action);
                case ActionTypeEnum.MtionStudio: return new MtionStudioActionViewModel((MtionStudioActionModel)action);
                case ActionTypeEnum.MusicPlayer: return new MusicPlayerActionEditorControlViewModel((MusicPlayerActionModel)action);
                case ActionTypeEnum.Overlay: return new OverlayActionEditorControlViewModel((OverlayActionModel)action);
                case ActionTypeEnum.PlatformMessage: return new PlatformMessageActionEditorControlViewModel((PlatformMessageActionModel)action);
                case ActionTypeEnum.PixelChat: return new PixelChatActionEditorControlViewModel((PixelChatActionModel)action);
                case ActionTypeEnum.PolyPop: return new PolyPopActionEditorControlViewModel((PolyPopActionModel)action);
                case ActionTypeEnum.RahiTuber: return new RahiTuberActionEditorControlViewModel((RahiTuberActionModel)action);
                case ActionTypeEnum.Random: return new RandomActionEditorControlViewModel((RandomActionModel)action);
                case ActionTypeEnum.Repeat: return new RepeatActionEditorControlViewModel((RepeatActionModel)action);
                case ActionTypeEnum.SAMMI: return new SAMMIActionEditorControlViewModel((SAMMIActionModel)action);
                case ActionTypeEnum.Script: return new ScriptActionEditorControlViewModel((ScriptActionModel)action);
                case ActionTypeEnum.Serial: return new SerialActionEditorControlViewModel((SerialActionModel)action);
                case ActionTypeEnum.Sound: return new SoundActionEditorControlViewModel((SoundActionModel)action);
                case ActionTypeEnum.SpecialIdentifier: return new SpecialIdentifierActionEditorControlViewModel((SpecialIdentifierActionModel)action);
                case ActionTypeEnum.StreamingSoftware: return new StreamingSoftwareActionEditorControlViewModel((StreamingSoftwareActionModel)action);
                case ActionTypeEnum.Streamlabs: return new StreamlabsActionEditorControlViewModel((StreamlabsActionModel)action);
                case ActionTypeEnum.TextToSpeech: return new TextToSpeechActionEditorControlViewModel((TextToSpeechActionModel)action);
                case ActionTypeEnum.TITS: return new TITSActionEditorControlViewModel((TITSActionModel)action);
                case ActionTypeEnum.Twitch: return new TwitchActionEditorControlViewModel((TwitchActionModel)action);
                case ActionTypeEnum.UserLookup: return new UserLookupActionEditorControlViewModel((UserLookupActionModel)action);
                case ActionTypeEnum.VConnect: return new VConnectActionEditorControlViewModel((VConnectActionModel)action);
                case ActionTypeEnum.Veadotube: return new VeadotubeActionEditorControlViewModel((VeadotubeActionModel)action);
                case ActionTypeEnum.Velora: return new VeloraActionEditorControlViewModel((VeloraActionModel)action);
                case ActionTypeEnum.VPZone: return new VPZoneActionEditorControlViewModel((VPZoneActionModel)action);
                case ActionTypeEnum.Voicemod: return new VoicemodActionEditorControlViewModel((VoicemodActionModel)action);
                case ActionTypeEnum.VTSPog: return new VTSPogActionEditorControlViewModel((VTSPogActionModel)action);
                case ActionTypeEnum.VTubeStudio: return new VTubeStudioActionEditorControlViewModel((VTubeStudioActionModel)action);
                case ActionTypeEnum.Wait: return new WaitActionEditorControlViewModel((WaitActionModel)action);
                case ActionTypeEnum.WebRequest: return new WebRequestActionEditorControlViewModel((WebRequestActionModel)action);
                case ActionTypeEnum.YouTube: return new YouTubeActionEditorControlViewModel((YouTubeActionModel)action);
            }
            return null;
        }

        public async Task<IEnumerable<Result>> ValidateActions()
        {
            List<Result> results = new List<Result>();
            foreach (ActionEditorControlViewModelBase actionViewModel in this.Actions)
            {
                results.Add(await actionViewModel.ValidateForCommandBuild());
            }
            return results;
        }

        public async Task<IEnumerable<ActionModelBase>> GetActions()
        {
            List<ActionModelBase> actions = new List<ActionModelBase>();
            foreach (ActionEditorControlViewModelBase actionViewModel in this.Actions)
            {
                ActionModelBase action = await actionViewModel.GetAction();
                if (action == null)
                {
                    return null;
                }
                actions.Add(action);
            }
            return actions;
        }

        private Task AddActionViewModel(ActionEditorControlViewModelBase editorViewModel)
        {
            if (editorViewModel != null)
            {
                editorViewModel.Initialize(this);
                this.Actions.Add(editorViewModel);
            }
            return Task.CompletedTask;
        }

        private bool IsNestedUnderAction(ActionEditorControlViewModelBase actionViewModel)
        {
            ActionEditorControlViewModelBase parentAction = this.ParentAction;
            while (parentAction != null)
            {
                if (object.Equals(parentAction, actionViewModel))
                {
                    return true;
                }

                parentAction = parentAction.ActionEditorListControlViewModel?.ParentAction;
            }
            return false;
        }

        private int GetSafeInsertIndex(int insertIndex)
        {
            if (insertIndex < 0)
            {
                return 0;
            }

            if (insertIndex > this.Actions.Count)
            {
                return this.Actions.Count;
            }
            return insertIndex;
        }
    }
}
