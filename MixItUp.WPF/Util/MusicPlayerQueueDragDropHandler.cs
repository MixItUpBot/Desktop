using GongSolutions.Wpf.DragDrop;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System.Collections.ObjectModel;
using System.Windows;

namespace MixItUp.WPF.Util
{
    public class MusicPlayerQueueDragDropHandler : IDropTarget
    {
        public static MusicPlayerQueueDragDropHandler Instance { get; } = new MusicPlayerQueueDragDropHandler();

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is MusicPlayerSong && dropInfo.TargetCollection != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is MusicPlayerSong sourceItem && dropInfo.TargetCollection != null)
            {
                var items = ServiceManager.Get<IMusicPlayerService>().Songs;
                if (items != null)
                {
                    int oldIndex = items.IndexOf(sourceItem);
                    int newIndex = dropInfo.InsertIndex;

                    if (oldIndex != -1)
                    {
                        if (newIndex > oldIndex)
                        {
                            newIndex--;
                        }

                        _ = ServiceManager.Get<IMusicPlayerService>().MoveInQueue(oldIndex, newIndex);
                    }
                }
            }
        }
    }
}
