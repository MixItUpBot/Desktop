using MixItUp.Base.ViewModel.User;

namespace MixItUp.Base.Model.Razo
{
    public class RazoGiftEventModel
    {
        public UserV2ViewModel User { get; set; }

        public int Amount { get; set; }

        public string Message { get; set; }

        public RazoGiftEventModel(UserV2ViewModel user, int amount, string message = null)
        {
            this.User = user;
            this.Amount = amount;
            this.Message = message;
        }
    }
}
