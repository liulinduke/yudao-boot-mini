UPDATE ai_workflow
SET graph = JSON_SET(
        JSON_SET(
            graph,
            '$.nodes[1].data.userPrompt',
            '翻译{{targetLanguage}}：{{text}}'
        ),
        '$.nodes[1].data.systemPrompt',
        '只输出译文，不要解释。'
    ),
    update_time = NOW()
WHERE code = 'fb_message_translate_v1'
  AND deleted = 0;
