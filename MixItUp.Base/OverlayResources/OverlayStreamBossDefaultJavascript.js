const main = document.getElementById("maindiv");

const bossImage = document.getElementById("bossImage");
const bossName = document.getElementById("bossName");
const bossHealth = document.getElementById("bossHealth");
const bossMaxHealth = document.getElementById("bossMaxHealth");
const bossHealthBarRemaining = document.getElementById("bossHealthBarRemaining");

function damage(data)
{
    bossHealth.textContent = data.BossHealth;
    adjustProgress(data);
    performAnimation("{DamageAnimationFramework}", "{DamageAnimationName}", main).then((result) =>
    {
        
    });
}

function heal(data)
{
    bossHealth.textContent = data.BossHealth;
    adjustProgress(data);
    performAnimation("{HealingAnimationFramework}", "{HealingAnimationName}", main).then((result) =>
    {
        
    });
}

function newboss(data)
{
    bossImage.src = data.BossImage;
    bossName.textContent = data.BossName;
    bossHealth.textContent = data.BossHealth;
    bossMaxHealth.textContent = data.BossMaxHealth;
    adjustProgress(data);
    performAnimation("{NewBossAnimationFramework}", "{NewBossAnimationName}", main).then((result) =>
    {
        
    });
}

function killboss(data)
{
    damage({
        "BossHealth": 0,
        "BossHealthBarRemaining": 0,
    })
    
    setTimeout(() => newboss(data), 3000);
}

function adjustProgress(data)
{
    bossHealthBarRemaining.style.setProperty("--startPercentage", bossHealthBarRemaining.style.width);
    bossHealthBarRemaining.style.setProperty("--endPercentage", data.BossHealthBarRemaining + "%");
    bossHealthBarRemaining.style.width = data.BossHealthBarRemaining + "%";
    bossHealthBarRemaining.classList.add("adjustHealth");
    setTimeout(() => {
        bossHealthBarRemaining.classList.remove("adjustHealth");
    }, 2000);
}

newboss({
    "BossImage": "{BossImage}",
    "BossName": "{BossName}",
    "BossHealth": {BossHealth},
    "BossMaxHealth": {BossMaxHealth},
    "BossHealthBarRemaining": {BossHealthBarRemaining},
});