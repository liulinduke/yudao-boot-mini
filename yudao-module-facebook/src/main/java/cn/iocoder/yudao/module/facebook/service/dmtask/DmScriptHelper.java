package cn.iocoder.yudao.module.facebook.service.dmtask;

import cn.hutool.core.util.StrUtil;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.ThreadLocalRandom;

/**
 * 私信话术辅助：打散分配 + Facebook 常用表情
 */
public final class DmScriptHelper {

    /** Facebook / Messenger 常用表情（Unicode） */
    private static final String[] FACEBOOK_EMOJIS = {
            "😀", "😃", "😄", "😁", "😊", "🙂", "😉", "😍", "🥰", "😘",
            "😋", "😎", "🤗", "🤩", "🥳", "👍", "👏", "🙌", "💪", "✨",
            "⭐", "🌟", "💯", "🔥", "❤️", "💙", "💚", "💛", "🧡", "💜",
            "🎉", "🎊", "🌹", "🌸", "☀️", "🌈", "😇", "🤝", "🙏", "💐"
    };

    private DmScriptHelper() {
    }

    /**
     * 为每条明细打散分配话术（轮询 + 随机起点，避免同一账号连续相同话术）
     */
    public static List<String> scatterScripts(List<String> scripts, int detailCount) {
        if (scripts == null || scripts.isEmpty() || detailCount <= 0) {
            return Collections.emptyList();
        }
        List<String> normalized = new ArrayList<>();
        for (String script : scripts) {
            if (StrUtil.isNotBlank(script)) {
                normalized.add(script.trim());
            }
        }
        if (normalized.isEmpty()) {
            return Collections.emptyList();
        }

        Collections.shuffle(normalized);
        int startOffset = ThreadLocalRandom.current().nextInt(normalized.size());

        List<String> result = new ArrayList<>(detailCount);
        for (int i = 0; i < detailCount; i++) {
            result.add(normalized.get((startOffset + i) % normalized.size()));
        }
        // 再次打乱明细级话术顺序，降低批量相同话术特征
        Collections.shuffle(result);
        return result;
    }

    /**
     * 在话术末尾追加 1~2 个随机 Facebook 表情
     */
    public static String appendRandomEmoji(String script) {
        if (StrUtil.isBlank(script)) {
            return script;
        }
        ThreadLocalRandom random = ThreadLocalRandom.current();
        int emojiCount = random.nextInt(1, 3);
        StringBuilder sb = new StringBuilder(script.trim());
        sb.append(' ');
        for (int i = 0; i < emojiCount; i++) {
            sb.append(FACEBOOK_EMOJIS[random.nextInt(FACEBOOK_EMOJIS.length)]);
        }
        return sb.toString();
    }

}
