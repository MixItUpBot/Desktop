const main = document.getElementById("maindiv");

const goalName = document.getElementById("goalName");
const goalEnd = document.getElementById("goalEnd");
const goalBarCompleted = document.getElementById("goalBarCompleted");
const goalAmount = document.getElementById("goalAmount");
const goalMaxAmount = document.getElementById("goalMaxAmount");

function update(data)
{
    goalAmount.textContent = data.GoalAmount;
    adjustProgress(data);
    performAnimation("{ProgressOccurredAnimationFramework}", "{ProgressOccurredAnimationName}", main).then((result) =>
    {
        
    });
}

function complete(data)
{
    update({
        "GoalAmount": goalMaxAmount.textContent,
        "GoalBarCompletionPercentage": 100,
    })
    
    setTimeout(() => reset(data), 3000);
}

function reset(data)
{
    goalName.textContent = data.GoalName;
    goalEnd.textContent = data.GoalEnd;
    goalAmount.textContent = data.GoalAmount;
    goalMaxAmount.textContent = data.GoalMaxAmount;
    adjustProgress(data);
    performAnimation("{SegmentCompletedAnimationFramework}", "{SegmentCompletedAnimationName}", main).then((result) =>
    {
        
    });
}

function adjustProgress(data)
{
    goalBarCompleted.style.setProperty("--startPercentage", goalBarCompleted.style.width);
    goalBarCompleted.style.setProperty("--endPercentage", data.GoalBarCompletionPercentage + "%");
    goalBarCompleted.style.width = data.GoalBarCompletionPercentage + "%";
    goalBarCompleted.classList.add("fillProgressBar");
    setTimeout(() => {
        goalBarCompleted.classList.remove("fillProgressBar");
    }, 2000);
}

reset({
    "GoalName": "{GoalName}",
    "GoalEnd": "{GoalEnd}",
    "GoalBarCompletionPercentage": {GoalBarCompletionPercentage},
    "GoalAmount": {GoalAmount},
    "GoalMaxAmount": {GoalMaxAmount},
});