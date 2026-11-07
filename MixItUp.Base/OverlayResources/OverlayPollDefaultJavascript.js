const mainContainer = document.getElementById("mainContainer");

const question = document.getElementById("question");
const total = document.getElementById("total");
const time = document.getElementById("time");

const options = document.getElementById("options");

const optionTemplate = document.querySelector("#option");

const optionData = new Map();

var instance = 0;
var totalSeconds = 0;

function newpoll(data)
{
    mainContainer.style.visibility = 'hidden';
    
    instance = Math.random();

    while (options.firstChild) {
        options.removeChild(options.lastChild);
    }
    optionData.clear();

    question.textContent = data.Question;
    total.textContent = "0 Votes";
    
    totalSeconds = data.TimeLimit;
    
    data.Options.forEach((od) =>
    {
        optionData.set(od.ID, od);
        
        let option = optionTemplate.content.cloneNode(true);
        option.firstElementChild.setAttribute("id", "option" + od.ID);
        
        let name = option.firstElementChild.querySelector(".name");
        name.textContent = od.Name;
        
        let bar = option.firstElementChild.querySelector(".bar");
        bar.style.backgroundColor = od.Color;
        
        if (data.ShowTwitchPredictionChannelPoints)
        {
            let channelPoints = option.querySelector(".channelPoints");
            channelPoints.style.visibility = 'visible';
        }

        options.appendChild(option);
    });
    
    mainContainer.style.visibility = 'visible';

    performAnimation("{EntranceAnimationFramework}", "{EntranceAnimationName}", mainContainer).then((result) =>
    {

    });
    
    timerLoop(instance);
}

function timerLoop(inst)
{
    let text = "MM:ss";

    let seconds = totalSeconds % 60;
    if (seconds < 10) { seconds = "0" + seconds }
    text = text.replace("SS", totalSeconds);
    text = text.replace("ss", seconds);

    let totalMinutes = Math.floor(totalSeconds / 60);
    let minutes = totalMinutes % 60;
    if (minutes < 10) { minutes = "0" + minutes }
    text = text.replace("MM", totalMinutes);
    text = text.replace("mm", minutes);

    let totalHours = Math.floor(totalMinutes / 60);
    let hours = totalHours % 24;
    if (hours < 10) { hours = "0" + hours }
    text = text.replace("HH", totalHours);
    text = text.replace("hh", hours);

    let days = Math.floor(totalHours / 24);
    text = text.replace("DD", hours);

    time.textContent = text;

    setTimeout(function () {
        if (totalSeconds > 0 && instance == inst)
        {
            totalSeconds--;
            timerLoop(inst);
        }
    }, 1000);
}

function update(data)
{
    total.textContent = `${data.TotalVotes} Votes`;

    data.Options.forEach((od) =>
    {
        if (optionData.has(od.ID))
        {
            let option = document.getElementById("option" + od.ID);
            
            let amount = option.querySelector(".amount");
            amount.textContent = `${od.Amount} Votes`;
            
            let percentage = option.querySelector(".percentage");
            percentage.textContent = `(${od.Percentage}%)`;
            
            let channelPointAmount = option.querySelector(".channelPointAmount");
            channelPointAmount.textContent = od.ChannelPoints;
            
            let previous = optionData.get(od.ID);
            optionData.set(od.ID, od);
            
            if (previous.Percentage != od.Percentage)
            {
                let bar = option.querySelector(".bar");
                adjustProgress(bar, od.Percentage);
            }
        }
    });
}

function end(data)
{
    if (optionData.has(data.WinnerID))
    {
        optionData.forEach((value, key) => {
            let option = document.getElementById("option" + key);
            let bar = option.querySelector(".bar");
            bar.style.backgroundColor = "DimGray";
        });
        
        let option = document.getElementById("option" + data.WinnerID);
        let bar = option.querySelector(".bar");
        bar.style.backgroundColor = "Gold";
    
        setTimeout(() =>
        {
            performAnimation("{ExitAnimationFramework}", "{ExitAnimationName}", mainContainer).then((result) =>
            {
                while (options.firstChild) {
                    options.removeChild(options.lastChild);
                }
                optionData.clear();
                mainContainer.style.visibility = 'hidden';
            });
        }, 5000);
    }
}

function adjustProgress(bar, newPercentage)
{
    bar.style.setProperty("--startPercentage", bar.style.width);
    bar.style.setProperty("--endPercentage", newPercentage + "%");
    bar.style.width = newPercentage + "%";
    bar.classList.add("adjustBar");
    setTimeout(() => {
        bar.classList.remove("adjustBar");
    }, 2000);
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });