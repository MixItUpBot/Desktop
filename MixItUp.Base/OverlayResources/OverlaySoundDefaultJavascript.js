const main = document.getElementById("maindiv");
const sound = document.getElementById('audio');

function removeSelf()
{
    sendParentMessage({ Type: "Remove", ID: "{ID}" });
}

function remove(data)
{
    removeSelf();
}

function soundFinished()
{
    sendParentMessage({ Type: "SoundFinished", ID: "{ID}" });
    removeSelf();
}

sound.addEventListener('ended', soundFinished);
sound.addEventListener('error', soundFinished, true);