UPDATE ai_workflow
SET graph = '{"edges":[{"id":"edge-message-translate-start-llm","source":"start-message-translate","target":"llm-message-translate"}],"nodes":[{"id":"start-message-translate","data":{"title":"开始","parameters":[{"id":"text","name":"text","dataType":"String","required":true,"description":"原文"},{"id":"targetLanguage","name":"targetLanguage","dataType":"String","required":true,"description":"目标语言"}],"description":"消息翻译输入"},"type":"startNode","position":{"x":80,"y":120}},{"id":"llm-message-translate","data":{"topK":10,"topP":0.9,"llmId":1,"title":"消息翻译","parameters":[{"id":"translate-param-target","ref":"start-message-translate.targetLanguage","name":"targetLanguage","refType":"ref","dataType":"String"},{"id":"translate-param-text","ref":"start-message-translate.text","name":"text","refType":"ref","dataType":"String"}],"userPrompt":"翻译{{targetLanguage}}：{{text}}","description":"直接返回目标语言翻译文本","temperature":0.2,"systemPrompt":"你是专业翻译。只返回翻译后的字符串，不要返回 JSON、引号、解释或 Markdown。保留产品名、数量、MOQ、型号、链接和原文语气。"},"type":"llmNode","position":{"x":420,"y":120}}]}',
    name = 'FB消息翻译 V2',
    remark = 'Facebook消息翻译：按目标语言直接返回字符串',
    update_time = NOW()
WHERE code = 'fb_message_translate_v1'
  AND deleted = 0;
