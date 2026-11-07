const messageTemplate = document.querySelector("#message");
const avatarTemplate = document.querySelector("#avatar");
const badgeTemplate = document.querySelector("#badge");
const pronounsTemplate = document.querySelector("#pronouns");
const usernameTemplate = document.querySelector("#username");
const textTemplate = document.querySelector("#text");
const emoteTemplate = document.querySelector("#emote");

const list = document.getElementById("list");

const maxMessages = 100;
var pendingMessage = [];

const messageDelayTime = {MessageDelayTime};
const messageRemovalTime = {MessageRemovalTime};
const addMessagesToTop = {AddMessagesToTop};
const displayAlejoPronouns = {DisplayAlejoPronouns};

function add(data)
{
    let message = messageTemplate.content.cloneNode(true);
    let messageSpan = message.querySelector(".message");
    
    let avatar = avatarTemplate.content.cloneNode(true);
    avatar.firstElementChild.src = data.User.AvatarLink;
    messageSpan.appendChild(avatar);

    addBadge(messageSpan, data.User.PlatformBadgeFullLink, {ShowPlatformBadge});
    addBadge(messageSpan, data.User.PlatformRoleBadgeLink, {ShowRoleBadge});
    addBadge(messageSpan, data.User.PlatformSubscriberBadgeLink, {ShowSubscriberBadge});
    addBadge(messageSpan, data.User.PlatformSpecialtyBadgeLink, {ShowSpecialtyBadge});
    
    if (displayAlejoPronouns && data.User.AlejoPronoun.length > 0)
    {
        let pronouns = pronounsTemplate.content.cloneNode(true);
        pronouns.firstElementChild.style.color = data.User.Color;
        pronouns.firstElementChild.textContent = `[${data.User.AlejoPronoun}]`;
        messageSpan.appendChild(pronouns);
    }
    
    let username = usernameTemplate.content.cloneNode(true);
    username.firstElementChild.style.color = data.User.Color;
    username.firstElementChild.textContent = data.User.DisplayName + ":";
    messageSpan.appendChild(username);
    
    data.Message.forEach(messagePart =>
    {
        if (messagePart.Type === "Text")
        {
            let text = textTemplate.content.cloneNode(true);
            text.firstElementChild.textContent = messagePart.Content;
            messageSpan.appendChild(text);
        }
        else if (messagePart.Type === "Emote")
        {
            let emote = emoteTemplate.content.cloneNode(true);
            emote.firstElementChild.src = messagePart.Content;
            messageSpan.appendChild(emote);
        }
    });

    let container = document.createElement("div");
    container.id = data.MessageID;
    container.setAttribute('username', data.User.Username);
    container.appendChild(message);
    
    pendingMessage.push(container);
    if (messageDelayTime > 0)
    {
        setTimeout(() => {
            addInternal(container);
        }, messageDelayTime * 1000);
    }
    else
    {
        addInternal(container);
    }
}

function remove(data)
{
    for (let i = 0; i < pendingMessage.length; i++)
    {
        if (pendingMessage[i].id == data.MessageID)
        {
            pendingMessage.removeChild(pendingMessage[i]);
            return;
        }
        else if (pendingMessage[i].getAttribute("username") == data.Username)
        {
            pendingMessage.removeChild(pendingMessage[i]);
            i--;
        }
    }
    
    for (let i = 0; i < list.children.length; i++)
    {
        if (list.children[i].id == data.MessageID)
        {
            list.removeChild(list.children[i]);
            return;
        }
        else if (list.children[i].getAttribute("username") == data.Username)
        {
            list.removeChild(list.children[i]);
            i--;
        }
    }
}

function clear(data)
{
    while (list.childElementCount > 0)
    {
        list.removeChild(list.lastElementChild);
    }
}

function addBadge(messageSpan, url, include)
{
    if (include && url != null && url.length > 0)
    {
        let badge = badgeTemplate.content.cloneNode(true);
        badge.firstElementChild.src = url;
        messageSpan.appendChild(badge);
    }
}

function addInternal(message)
{
    if (!pendingMessage.includes(message))
    {
        return
    }
    pendingMessage.splice(message, 1);
    
    if (addMessagesToTop)
    {
        list.insertBefore(message, list.firstChild);
    }
    else
    {
        list.appendChild(message);
    }
    
    performAnimation("{MessageAddedAnimationFramework}", "{MessageAddedAnimationName}", message).then((result) =>
    {
        
    });
    
    if (messageRemovalTime > 0)
    {
        setTimeout(() => {
            removeInternal(message);
        }, messageRemovalTime * 1000);
    }
    
    if (list.childElementCount > maxMessages)
    {
        if (addMessagesToTop)
        {
            list.removeChild(list.lastChild);
        }
        else
        {
            list.removeChild(list.firstChild);
        }
    }
}

function removeInternal(message)
{
    performAnimation("{MessageRemovedAnimationFramework}", "{MessageRemovedAnimationName}", message).then((result) =>
    {
        list.removeChild(message);
    });
}
