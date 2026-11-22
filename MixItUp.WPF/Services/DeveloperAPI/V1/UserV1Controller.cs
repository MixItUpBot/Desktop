using MixItUp.API.V1.Models;
using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V1
{
    [Route("api/users")]
    [ApiController]
    public class UserV1Controller : ControllerBase
    {
        public static async Task<UserV2ViewModel> GetUserData(StreamingPlatformTypeEnum platform, string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = null;
            if (!string.IsNullOrEmpty(usernameOrID))
            {
                if (Guid.TryParse(usernameOrID, out Guid userId))
                {
                    user = await ServiceManager.Get<UserService>().GetUserByID(platform, userId);
                }
                else
                {
                    user = await ServiceManager.Get<UserService>().GetUserByPlatform(platform, platformID: usernameOrID, platformUsername: usernameOrID);
                }
            }
            return user;
        }

        [HttpPost]
        public async Task<IEnumerable<User>> BulkGet([FromBody] IEnumerable<string> usernamesOrIDs)
        {
            List<User> users = new List<User>();
            if (usernamesOrIDs == null)
            {
                return users;
            }

            foreach (var usernameOrID in usernamesOrIDs)
            {
                UserV2ViewModel user = await UserV1Controller.GetUserData(ChannelSession.Settings.DefaultStreamingPlatform, usernameOrID);
                if (user != null)
                {
                    users.Add(UserFromUserDataViewModel(user));
                }
            }

            return users;
        }

        [Route("{usernameOrID}")]
        [HttpGet]
        public async Task<IActionResult> Get(string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(ChannelSession.Settings.DefaultStreamingPlatform, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        [Route("twitch/{usernameOrID}")]
        [HttpGet]
        public async Task<IActionResult> GetTwitch(string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(StreamingPlatformTypeEnum.Twitch, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        [Route("youtube/{usernameOrID}")]
        [HttpGet]
        public async Task<IActionResult> GetYouTube(string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(StreamingPlatformTypeEnum.YouTube, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        [Route("kick/{usernameOrID}")]
        [HttpGet]
        public async Task<IActionResult> GetKick(string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(StreamingPlatformTypeEnum.Kick, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        [Route("joystick/{usernameOrID}")]
        [HttpGet]
        public async Task<IActionResult> GetJoystick(string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(StreamingPlatformTypeEnum.Joystick, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        [Route("velora/{usernameOrID}")]
        [HttpGet]
        public async Task<IActionResult> GetVelora(string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(StreamingPlatformTypeEnum.Velora, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        [HttpGet]
        public async Task<IActionResult> GetVPZone(string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(StreamingPlatformTypeEnum.VPZone, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        [Route("{usernameOrID}")]
        [HttpPut, HttpPatch]
        public async Task<IActionResult> Update(string usernameOrID, [FromBody] User updatedUserData)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(ChannelSession.Settings.DefaultStreamingPlatform, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return await UpdateUser(user, updatedUserData);
        }

        private async Task<IActionResult> UpdateUser(UserV2ViewModel user, User updatedUserData)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (updatedUserData == null || !updatedUserData.ID.Equals(user.ID))
            {
                return BadRequest(new Error { Message = "Unable to parse update data from POST body." });
            }

            if (updatedUserData.ViewingMinutes.HasValue)
            {
                user.OnlineViewingMinutes = updatedUserData.ViewingMinutes.Value;
            }

            foreach (CurrencyAmount currencyData in updatedUserData.CurrencyAmounts)
            {
                if (ChannelSession.Settings.Currency.ContainsKey(currencyData.ID))
                {
                    ChannelSession.Settings.Currency[currencyData.ID].SetAmount(user, currencyData.Amount);
                }
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        [Route("{usernameOrID}/currency/{currencyID:guid}/adjust")]
        [HttpPut, HttpPatch]
        public async Task<IActionResult> AdjustUserCurrency(string usernameOrID, Guid currencyID, [FromBody] AdjustCurrency currencyUpdate)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(ChannelSession.Settings.DefaultStreamingPlatform, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }

            return AdjustCurrency(user, currencyID, currencyUpdate);
        }

        [Route("{usernameOrID}/inventory/{inventoryID:guid}/adjust")]
        [HttpPut, HttpPatch]
        public async Task<IActionResult> AdjustUserInventory(string usernameOrID, Guid inventoryID, [FromBody] AdjustInventory inventoryUpdate)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            UserV2ViewModel user = await UserV1Controller.GetUserData(ChannelSession.Settings.DefaultStreamingPlatform, usernameOrID);
            if (user == null)
            {
                return NotFound(new Error { Message = $"Unable to find user: {usernameOrID}." });
            }
            return AdjustInventory(user, inventoryID, inventoryUpdate);
        }

        [Route("top")]
        [HttpGet]
        public async Task<IActionResult> Get(int count = 10)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (count < 1)
            {
                return BadRequest(new Error { Message = $"Count must be greater than 0." });
            }

            Dictionary<Guid, UserV2Model> allUsersDictionary = ChannelSession.Settings.Users.ToDictionary();

            IEnumerable<UserV2Model> allUsers = allUsersDictionary.Select(kvp => kvp.Value);
            allUsers = allUsers.Where(u => !u.IsSpecialtyExcluded);

            List<User> userList = new List<User>();
            foreach (UserV2Model user in allUsers.OrderByDescending(u => u.OnlineViewingMinutes).Take(count))
            {
                userList.Add(UserFromUserDataViewModel(new UserV2ViewModel(user)));
            }
            return Ok(userList);
        }

        public static User UserFromUserDataViewModel(UserV2ViewModel userData)
        {
            User user = new User
            {
                ID = userData.ID,
                TwitchID = userData.Model.GetPlatformID(StreamingPlatformTypeEnum.Twitch),
                YouTubeID = userData.Model.GetPlatformID(StreamingPlatformTypeEnum.YouTube),
                KickID = userData.Model.GetPlatformID(StreamingPlatformTypeEnum.Kick),
                VeloraID = userData.Model.GetPlatformID(StreamingPlatformTypeEnum.Velora),
                VPZoneID = userData.Model.GetPlatformID(StreamingPlatformTypeEnum.VPZone),
                JoystickID = userData.Model.GetPlatformID(StreamingPlatformTypeEnum.Joystick),
                RazoID = userData.Model.GetPlatformID(StreamingPlatformTypeEnum.Razo),
                AltStreamID = userData.Model.GetPlatformID(StreamingPlatformTypeEnum.AltStream),
                Username = userData.Model.GetPlatformUsername(ChannelSession.Settings.DefaultStreamingPlatform),
                ViewingMinutes = userData.OnlineViewingMinutes
            };

            foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
            {
                user.CurrencyAmounts.Add(CurrencyV1Controller.CurrencyAmountFromUserCurrencyViewModel(currency, currency.GetAmount(userData)));
            }

            foreach (InventoryModel inventory in ChannelSession.Settings.Inventory.Values)
            {
                user.InventoryAmounts.Add(InventoryV1Controller.InventoryAmountFromUserInventoryViewModel(inventory, inventory.GetAmounts(userData)));
            }

            return user;
        }

        private IActionResult AdjustCurrency(UserV2ViewModel user, Guid currencyID, [FromBody] AdjustCurrency currencyUpdate)
        {
            if (!ChannelSession.Settings.Currency.ContainsKey(currencyID))
            {
                return NotFound(new Error { Message = $"Unable to find currency: {currencyID.ToString()}." });
            }

            if (currencyUpdate == null)
            {
                return BadRequest(new Error { Message = "Unable to parse currency adjustment from POST body." });
            }

            CurrencyModel currency = ChannelSession.Settings.Currency[currencyID];

            if (currencyUpdate.Amount < 0)
            {
                int quantityToRemove = currencyUpdate.Amount * -1;
                if (!currency.HasAmount(user, quantityToRemove))
                {
                    return StatusCode(403, new Error { Message = "User does not have enough currency to remove" });
                }

                currency.SubtractAmount(user, quantityToRemove);
            }
            else if (currencyUpdate.Amount > 0)
            {
                currency.AddAmount(user, currencyUpdate.Amount);
            }

            return Ok(UserFromUserDataViewModel(user));
        }

        private IActionResult AdjustInventory(UserV2ViewModel user, Guid inventoryID, [FromBody] AdjustInventory inventoryUpdate)
        {
            if (!ChannelSession.Settings.Inventory.ContainsKey(inventoryID))
            {
                return NotFound(new Error { Message = $"Unable to find inventory: {inventoryID.ToString()}." });
            }

            if (inventoryUpdate == null)
            {
                return BadRequest(new Error { Message = "Unable to parse inventory adjustment from POST body." });
            }

            InventoryModel inventory = ChannelSession.Settings.Inventory[inventoryID];

            if (string.IsNullOrEmpty(inventoryUpdate.Name) || !inventory.ItemExists(inventoryUpdate.Name))
            {
                return BadRequest(new Error { Message = "Unable to find requested inventory item." });
            }

            InventoryItemModel item = inventory.GetItem(inventoryUpdate.Name);
            if (inventoryUpdate.Amount < 0)
            {
                int quantityToRemove = inventoryUpdate.Amount * -1;
                if (!inventory.HasAmount(user, item.ID, quantityToRemove))
                {
                    return StatusCode(403, new Error { Message = "User does not have enough inventory to remove" });
                }

                inventory.SubtractAmount(user, item.ID, quantityToRemove);
            }
            else if (inventoryUpdate.Amount > 0)
            {
                inventory.AddAmount(user, item.ID, inventoryUpdate.Amount);
            }

            return Ok(UserFromUserDataViewModel(user));
        }
    }
}
