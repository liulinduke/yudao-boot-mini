(function () {
  return new Promise(async function (resolve) {
    const config = __FOLLOW_CONFIG_JSON__;
    const result = {
      accountId: String(config.accountId || ''),
      postUrl: config.targetUrl || config.postUrl || '',
      targetType: 'page',
      targetUrl: config.targetUrl || config.postUrl || '',
      actionType: 7,
      status: 2,
      failReason: '',
      remark: ''
    };

    const randomDelay = (min, max) => new Promise((done) => {
      const delay = Math.floor(min + Math.random() * Math.max(1, max - min));
      setTimeout(done, delay);
    });
    const normalize = (text) => String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const isVisible = (el) => {
      if (!el) return false;
      const rect = el.getBoundingClientRect();
      const style = window.getComputedStyle(el);
      return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    };
    const fireMouseClick = async (el) => {
      el.scrollIntoView({ block: 'center', inline: 'center' });
      await randomDelay(300, 800);
      const rect = el.getBoundingClientRect();
      const x = rect.left + rect.width * (0.35 + Math.random() * 0.3);
      const y = rect.top + rect.height * (0.35 + Math.random() * 0.3);
      const opts = { view: window, bubbles: true, cancelable: true, clientX: x, clientY: y };
      el.dispatchEvent(new MouseEvent('mousemove', opts));
      await randomDelay(80, 180);
      el.dispatchEvent(new MouseEvent('mousedown', opts));
      await randomDelay(80, 180);
      el.dispatchEvent(new MouseEvent('mouseup', opts));
      el.dispatchEvent(new MouseEvent('click', opts));
    };

    const followWords = [
      'follow',
      '关注',
      '追蹤',
      '追踪',
      'theo dõi',
      'seguir',
      'abonn',
      's’abonner',
      'suscribirse',
      'segui'
    ];
    const followedWords = [
      'following',
      'followed',
      '已关注',
      '关注中',
      '正在追蹤',
      'đang theo dõi',
      'siguiendo',
      'abonné',
      'seguendo'
    ];
    const excludedWords = [
      'message',
      'watch',
      'more',
      'settings',
      'filters',
      'like',
      'comment',
      'share',
      '消息',
      '观看',
      '更多',
      '設定',
      '设置',
      '赞',
      '评论',
      '分享'
    ];

    const getLabel = (el) => {
      const directLabel = normalize([
        el.getAttribute('aria-label'),
        el.innerText,
        el.textContent
      ].filter(Boolean).join(' '));
      if (directLabel) {
        return directLabel;
      }
      return normalize(el.parentElement && el.parentElement.innerText);
    };
    const matchesAny = (label, words) => words.some((word) => label.includes(normalize(word)));
    const isExcluded = (label) => matchesAny(label, excludedWords);
    const getActionRoots = () => {
      const roots = [];
      const profileActions = document.querySelector('[data-pagelet="ProfileActions"]');
      if (profileActions) roots.push(profileActions);
      const main = document.querySelector('[role="main"]');
      if (main) roots.push(main);
      roots.push(document.body);
      return roots.filter(Boolean);
    };
    const getCandidates = (root) => Array.from(root.querySelectorAll('div[role="button"], button, a[role="button"]'))
      .filter(isVisible)
      .map((el) => ({ el, label: getLabel(el), rect: el.getBoundingClientRect() }))
      .filter((item) => item.rect.top >= 0 && item.rect.top < Math.max(window.innerHeight, 900));
    const findFollowButton = () => {
      for (const root of getActionRoots()) {
        const candidates = getCandidates(root);
        const exact = candidates.find((item) => item.label === 'follow');
        if (exact) return exact.el;
        const matched = candidates.find((item) =>
          matchesAny(item.label, followWords) &&
          !matchesAny(item.label, followedWords) &&
          !isExcluded(item.label)
        );
        if (matched) return matched.el;
      }
      return null;
    };
    const isAlreadyFollowed = () => {
      for (const root of getActionRoots()) {
        const candidates = getCandidates(root);
        if (candidates.some((item) => matchesAny(item.label, followedWords))) {
          return true;
        }
      }
      return false;
    };

    try {
      const range = Array.isArray(config.intervalRangeSeconds) ? config.intervalRangeSeconds : [30, 60];
      const minMs = Math.max(0, Number(range[0] || 0) * 1000);
      const maxMs = Math.max(minMs + 1000, Number(range[1] || range[0] || 1) * 1000);
      await randomDelay(minMs, maxMs);

      if (isAlreadyFollowed()) {
        result.status = 1;
        result.remark = '已是关注状态';
        resolve(JSON.stringify([result]));
        return;
      }

      const button = findFollowButton();
      if (!button) {
        result.failReason = '未找到关注按钮';
        resolve(JSON.stringify([result]));
        return;
      }

      await fireMouseClick(button);
      await randomDelay(1800, 3000);

      if (isAlreadyFollowed() || !findFollowButton()) {
        result.status = 1;
        result.remark = '已关注';
      } else {
        result.status = 3;
        result.remark = '已点击关注，等待页面确认';
      }
      resolve(JSON.stringify([result]));
    } catch (error) {
      result.status = 2;
      result.failReason = error && error.message ? error.message : String(error);
      resolve(JSON.stringify([result]));
    }
  });
})();
