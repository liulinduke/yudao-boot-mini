UPDATE ai_workflow
SET graph = JSON_SET(
        graph,
        '$.nodes[1].data.userPrompt',
        '翻译成{{targetLanguage}}：{{text}}'
    ),
    update_time = NOW()
WHERE code = 'fb_message_translate_v1'
  AND deleted = 0;
