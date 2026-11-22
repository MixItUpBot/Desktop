using MixItUp.Base.ViewModel.User;

namespace MixItUp.Base.Model.Joystick
{
    public class JoystickTippedEventModel
    {
        public UserV2ViewModel User { get; set; }

        public int Amount { get; set; }

        public string Message { get; set; }

        public JoystickTippedEventModel(UserV2ViewModel user, int amount, string message = null)
        {
            this.User = user;
            this.Amount = amount;
            this.Message = message;
        }
    }
}
