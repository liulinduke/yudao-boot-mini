UPDATE ai_workflow
SET graph = JSON_SET(
        graph,
        '$.nodes[1].data.systemPrompt',
        '只返回翻译后的字符串，不要返回 JSON、引号、解释或 Markdown。保留产品名、数量、MOQ、型号、链接和原文语气。'
    ),
    update_time = NOW()
WHERE code = 'fb_message_translate_v1'
  AND deleted = 0;
