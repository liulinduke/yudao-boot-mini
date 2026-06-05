console.log('ADD GROUP SCRIPT STARTED');
console.log('GROUP_LIST:', GROUP_LIST);
console.log('CURRENT URL:', window.location.href);

function randomDelay(min, max) {
    return new Promise(function(resolve) {
        setTimeout(resolve, min + Math.floor(Math.random() * (max - min)));
    });
}

function isJoined() {
    var allSpans = document.querySelectorAll('span');
    for (var i = 0; i < allSpans.length; i++) {
        var text = allSpans[i].textContent;
        if (text && text.trim() === 'Joined') {
            return true;
        }
    }
    return false;
}

function findJoinButton() {
    var joinButton = document.querySelector('[aria-label*="Join"]');
    if (!joinButton) {
        var buttons = document.querySelectorAll('button');
        for (var i = 0; i < buttons.length; i++) {
            var text = buttons[i].textContent;
            if (text && text.trim().toLowerCase() === 'join') {
                joinButton = buttons[i];
                break;
            }
        }
    }
    if (!joinButton) {
        joinButton = document.querySelector('[aria-label*="group"]');
    }
    return joinButton;
}

function pushResult(group, joinStatus, failReason) {
    results.push({
        accountId: ACCOUNT_ID || '',
        targetUrl: group.groupUrl,
        groupId: String(group.groupId || ''),
        groupName: group.groupName || '',
        groupUrl: group.groupUrl || '',
        joinStatus: joinStatus,
        failReason: failReason || '',
        joinTime: new Date().toISOString(),
        syncTime: new Date().toISOString()
    });
}

async function processGroup(group, index, total) {
    console.log('Processing group ' + (index + 1) + '/' + total + ': ' + group.groupName);
    await randomDelay(2000, 3000);

    if (isJoined()) {
        console.log('Already joined: ' + group.groupName);
        pushResult(group, 3, '');
        return;
    }

    var joinButton = findJoinButton();
    if (!joinButton) {
        console.log('Join button not found: ' + group.groupName);
        pushResult(group, 2, 'No join button found');
        return;
    }

    joinButton.click();
    console.log('Clicked join button for: ' + group.groupName);
    await randomDelay(3000, 4000);

    var joinedAfterClick = isJoined();
    pushResult(group, joinedAfterClick ? 1 : 1, joinedAfterClick ? '' : 'Join button clicked');
    console.log('Group done: ' + group.groupName);
}

async function execute() {
    if (!GROUP_LIST || GROUP_LIST.length === 0) {
        console.warn('GROUP_LIST is empty');
        resolve(JSON.stringify(results));
        return;
    }

    for (var i = 0; i < GROUP_LIST.length; i++) {
        await processGroup(GROUP_LIST[i], i, GROUP_LIST.length);
        if (i < GROUP_LIST.length - 1) {
            await randomDelay(3000, 5000);
        }
    }

    console.log('Add group task completed, results:', results.length);
    resolve(JSON.stringify(results));
}

execute().catch(function(err) {
    console.error('Add group task error:', err);
    reject(err);
});
