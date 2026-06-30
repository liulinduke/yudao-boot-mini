(function () {
  return new Promise(async function (resolve) {
    const config = __DEEP_PROFILE_CONFIG_JSON__;
    const sleep = (ms) => new Promise((done) => setTimeout(done, ms));
    const text = (el) => String((el && (el.innerText || el.textContent)) || '').replace(/\s+/g, ' ').trim();
    const clean = (value) => String(value || '').replace(/\s+/g, ' ').trim();
    const uniq = (items) => Array.from(new Set(items.map(clean).filter(Boolean)));
    const first = (items) => uniq(items)[0] || '';

    const result = {
      id: '',
      sourceUserId: config && config.sourceUserId ? String(config.sourceUserId) : '',
      fbUserId: '',
      userName: '',
      avatar: '',
      url: location.href.split('#')[0],
      dataType: 1,
      category: '',
      followers: null,
      city: '',
      location: '',
      hometown: '',
      phonenumber: '',
      phonenumber2: '',
      email: '',
      email2: '',
      whatsapp: '',
      line: '',
      website: '',
      profileStatus: '',
      gender: '',
      lastPostTime: null,
      lastPostSummary: '',
      deepCollected: true,
      fromResource: 'deep_collect',
      syncTime: null,
      config: ''
    };

    const getMeta = (name) => {
      const el = document.querySelector(`meta[property="${name}"], meta[name="${name}"]`);
      return el ? clean(el.getAttribute('content')) : '';
    };

    const canonicalize = (url) => {
      try {
        const parsed = new URL(url, location.origin);
        parsed.hash = '';
        return parsed.toString();
      } catch {
        return clean(url);
      }
    };
    const toLocalDateTime = (date) => {
      const pad = (n) => String(n).padStart(2, '0');
      return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
    };
    const parseFacebookDate = (raw) => {
      const value = clean(raw).replace(/\bat\b/i, '');
      if (!value) return null;
      const currentYear = new Date().getFullYear();
      const hasYear = /\b(19|20)\d{2}\b/.test(value);
      let parsed = new Date(hasYear ? value : `${value} ${currentYear}`);
      if (!Number.isNaN(parsed.getTime())) return parsed;
      parsed = new Date(value);
      if (!Number.isNaN(parsed.getTime())) return parsed;
      return null;
    };

    const extractProfileId = () => {
      const url = new URL(location.href);
      if (url.searchParams.get('id')) return url.searchParams.get('id');
      const entityMatch = document.documentElement.innerHTML.match(/"entity_id":"?(\d+)"?/);
      if (entityMatch) return entityMatch[1];
      const pageIdMatch = document.documentElement.innerHTML.match(/"page_id":"?(\d+)"?/);
      return pageIdMatch ? pageIdMatch[1] : '';
    };

    const parseFollowers = (value) => {
      const raw = clean(value).toLowerCase();
      if (!raw) return null;
      const match = raw.match(/([\d.,]+)/);
      if (!match) return null;
      let num = parseFloat(match[1].replace(/,/g, ''));
      if (!Number.isFinite(num)) return null;
      if (raw.includes('k') || raw.includes('千') || raw.includes('rb')) num *= 1000;
      else if (raw.includes('千万')) num *= 10000000;
      else if (raw.includes('亿') || raw.includes('億') || raw.includes('억')) num *= 100000000;
      else if (raw.includes('m') || raw.includes('百万') || raw.includes('jt')) num *= 1000000;
      else if (raw.includes('万') || raw.includes('만')) num *= 10000;
      const normalized = Math.floor(num);
      return normalized > 0 && normalized <= 1000000000 ? normalized : null;
    };

    const getIntroRoot = () => {
      const pagelets = Array.from(document.querySelectorAll('[data-pagelet]'));
      const intro = pagelets.find((el) => /^Intro\b|^简介\b|^簡介\b/i.test(text(el)))
        || pagelets.find((el) => /ProfileTilesFeed|intro|about/i.test(el.getAttribute('data-pagelet') || '') && /Intro|简介|簡介|Page ·|電話|Phone|Email|WhatsApp/i.test(text(el)));
      if (intro) return intro;
      const headings = Array.from(document.querySelectorAll('[role="heading"], h1, h2, h3, span, div'));
      const introHeading = headings.find((el) => /^(intro|简介|簡介|about)$/i.test(text(el)));
      return introHeading ? introHeading.closest('[data-pagelet]') || introHeading.closest('[role="region"]') || document.querySelector('[role="main"]') || document.body : document.querySelector('[role="main"]') || document.body;
    };

    const collectIntroLines = () => {
      const root = getIntroRoot();
      const rawLines = String((root && root.innerText) || '')
        .split(/\n+/)
        .map(clean)
        .filter((line) => line && line.length >= 2 && line.length <= 300);
      const nodes = Array.from(root.querySelectorAll('a, span, div'))
        .map(text)
        .filter((line) => line && line.length >= 2 && line.length <= 300);
      return uniq(rawLines.concat(nodes));
    };

    const classifyIntro = (lines) => {
      const fullText = lines.join('\n');
      const anchors = Array.from(document.querySelectorAll('a[href]'));
      const decodedHrefs = anchors.map((a) => {
        const href = a.href || '';
        try {
          const parsed = new URL(href);
          return parsed.searchParams.get('u') || href;
        } catch {
          return href;
        }
      });
      const telPhones = anchors
        .map((a) => a.getAttribute('href') || '')
        .filter((href) => /^tel:/i.test(href))
        .map((href) => href.replace(/^tel:/i, ''));
      const whatsappPhones = decodedHrefs
        .map((href) => {
          try {
            const parsed = new URL(href);
            return parsed.searchParams.get('phone') || '';
          } catch {
            return '';
          }
        })
        .filter(Boolean);
      const mailEmails = anchors
        .map((a) => a.getAttribute('href') || '')
        .filter((href) => /^mailto:/i.test(href))
        .map((href) => href.replace(/^mailto:/i, '').split('?')[0]);
      const textPhones = lines
        .filter((line) => !/@/.test(line))
        .flatMap((line) => line.match(/(?:\+|00)?[\d][\d\s().-]{6,}\d/g) || []);
      const emails = uniq((fullText.match(/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/gi) || []).concat(mailEmails));
      const phones = uniq(telPhones.concat(whatsappPhones).concat(textPhones));
      const links = uniq(decodedHrefs
        .filter((href) => /^https?:\/\//i.test(href))
        .filter((href) => !/facebook\.com|fb\.com|messenger\.com|whatsapp\.com|wa\.me/i.test(href))
        .map(canonicalize));

      result.email = emails[0] || '';
      result.email2 = emails[1] || '';
      result.phonenumber = phones[0] || '';
      result.phonenumber2 = phones[1] || '';
      result.website = links[0] || '';

      const whatsappLine = lines.find((line) => /whats\s*app|wa\b|wa\.me/i.test(line));
      const lineLine = lines.find((line) => /\bline\b|line id/i.test(line));
      result.whatsapp = first(whatsappPhones.concat(whatsappLine ? [whatsappLine] : []));
      result.line = lineLine || '';

      const categoryLine = lines.find((line) => /^Page\s*·|主页\s*·|公共主页\s*·/i.test(line));
      if (categoryLine) {
        result.category = categoryLine.replace(/^Page\s*·\s*/i, '').replace(/^主页\s*·\s*/i, '').replace(/^公共主页\s*·\s*/i, '');
      }

      const locationLine = lines.find((line) => /所在地|居住|住在|located in|based in|lives in|from |来自|家乡|hometown/i.test(line));
      if (locationLine) {
        if (/家乡|hometown|from |来自/i.test(locationLine)) result.hometown = locationLine;
        else if (/居住|住在|lives in/i.test(locationLine)) result.location = locationLine;
        else result.city = locationLine;
      }

      const genderLine = lines.find((line) => /male|female|男|女|性别|gender/i.test(line));
      result.gender = genderLine || '';

      const followerLine = lines.find((line) => /followers|粉丝|追蹤者|关注者/i.test(line));
      result.followers = parseFollowers(followerLine);

      const introCandidate = lines.find((line) =>
        line.length > 12 &&
        !/^Intro$/i.test(line) &&
        !/^Page\s*·|^主页\s*·|^公共主页\s*·/i.test(line) &&
        !emails.some((email) => line.includes(email)) &&
        !phones.some((phone) => line.includes(phone)) &&
        !/followers|粉丝|追蹤者|关注者|message|follow|always open|price range|reviews/i.test(line)
      );
      result.profileStatus = introCandidate || '';
    };

    const collectRecentPost = () => {
      const articles = Array.from(document.querySelectorAll('[role="article"]')).filter((el) => text(el).length > 20);
      const article = articles[0];
      if (!article) return;
      const summary = text(article).slice(0, 500);
      result.lastPostSummary = summary;
      const timeLink = Array.from(article.querySelectorAll('a[href]')).find((a) =>
        /\/posts\/|\/reel\/|permalink/i.test(a.href || '') &&
        /(\d+[mhdw]|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec|年|月|日|at)/i.test(text(a))
      );
      const timeEl = timeLink || article.querySelector('a[href*="/posts/"] span, a[href*="/reel/"] span, a[href*="permalink"] span, abbr, time');
      const rawTime = clean((timeEl && (timeEl.getAttribute('title') || timeEl.getAttribute('aria-label') || text(timeEl))) || '');
      const parsed = parseFacebookDate(rawTime);
      if (parsed && !Number.isNaN(parsed.getTime())) {
        result.lastPostTime = toLocalDateTime(parsed);
      }
      result.config = JSON.stringify({ recentPostRawTime: rawTime, introLines: collectIntroLines().slice(0, 80) });
    };

    try {
      await sleep(1200);
      result.userName = clean(document.querySelector('h1') && document.querySelector('h1').innerText) || getMeta('og:title') || document.title.replace(/\| Facebook.*$/i, '').trim();
      result.syncTime = toLocalDateTime(new Date());
      result.avatar = getMeta('og:image');
      result.url = canonicalize(getMeta('og:url') || location.href);
      result.fbUserId = extractProfileId();
      result.id = result.fbUserId;
      const lines = collectIntroLines();
      classifyIntro(lines);
      collectRecentPost();
      resolve(JSON.stringify([result]));
    } catch (error) {
      result.config = JSON.stringify({ error: error && error.message ? error.message : String(error) });
      resolve(JSON.stringify([result]));
    }
  });
})();
