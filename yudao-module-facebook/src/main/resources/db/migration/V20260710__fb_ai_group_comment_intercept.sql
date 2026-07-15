SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_collect_user' AND COLUMN_NAME = 'comment_content') = 0,
    'ALTER TABLE fb_collect_user ADD COLUMN comment_content TEXT NULL COMMENT ''评论内容'' AFTER config',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_collect_user' AND COLUMN_NAME = 'source_post_id') = 0,
    'ALTER TABLE fb_collect_user ADD COLUMN source_post_id BIGINT NULL COMMENT ''来源帖子ID'' AFTER comment_content',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_collect_user' AND COLUMN_NAME = 'source_post_url') = 0,
    'ALTER TABLE fb_collect_user ADD COLUMN source_post_url VARCHAR(1024) NULL COMMENT ''来源帖子URL'' AFTER source_post_id',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO ai_workflow
(id, name, code, graph, remark, status, create_time, update_time, deleted, creator, updater, tenant_id)
SELECT 2073000000000000001,
       'FB AI 群帖评论截流帖子筛选 V1',
       'fb_ai_group_comment_post_filter_v1',
       '{"edges":[{"id":"edge-group-comment-post-start-llm","source":"start-group-comment-post","target":"llm-group-comment-post-filter"}],"nodes":[{"id":"start-group-comment-post","data":{"title":"开始","parameters":[{"id":"exportProduct","name":"exportProduct","dataType":"String","required":true,"description":"用户主营/出口产品"},{"id":"persona","name":"persona","dataType":"String","required":false,"description":"业务员人设","defaultValue":"Professional"},{"id":"needComment","name":"needComment","dataType":"Boolean","required":false,"description":"是否需要评论话术","defaultValue":"false"},{"id":"needDm","name":"needDm","dataType":"Boolean","required":false,"description":"是否需要私信话术","defaultValue":"true"},{"id":"posts","name":"posts","dataType":"Array","required":true,"description":"群帖批次，建议每批 50 条"}],"description":"评论截流帖子筛选输入参数"},"type":"startNode","position":{"x":80,"y":120}},{"id":"llm-group-comment-post-filter","data":{"topK":50,"topP":0.9,"llmId":1,"title":"同行广告帖筛选","parameters":[{"id":"group-comment-post-param-exportProduct","ref":"start-group-comment-post.exportProduct","name":"exportProduct","refType":"ref","dataType":"String","description":"用户主营/出口产品"},{"id":"group-comment-post-param-persona","ref":"start-group-comment-post.persona","name":"persona","refType":"ref","dataType":"String","description":"业务员人设"},{"id":"group-comment-post-param-needComment","ref":"start-group-comment-post.needComment","name":"needComment","refType":"ref","dataType":"Boolean","description":"是否需要评论话术"},{"id":"group-comment-post-param-needDm","ref":"start-group-comment-post.needDm","name":"needDm","refType":"ref","dataType":"Boolean","description":"是否需要私信话术"},{"id":"group-comment-post-param-posts","ref":"start-group-comment-post.posts","name":"posts","refType":"ref","dataType":"Array","description":"群帖批次，建议每批 50 条"}],"userPrompt":"exportProduct={{exportProduct}}\npersona={{persona}}\nneedComment={{needComment}}\nneedDm={{needDm}}\nposts={{posts}}","description":"筛选适合评论截流的同行广告/推广帖","temperature":0.3,"systemPrompt":"你是一名 Facebook 群帖评论截流分析专家。\n\n用户主营/出口产品：{{exportProduct}}\n\n请判断群帖是否适合做评论截流：\n1. 帖子是否像同行、供应商、批发商、经销商、广告主发布的推广/供货/招商/产品介绍帖；\n2. 评论区是否可能出现咨询价格、库存、采购、联系方式、代理合作的人；\n3. 根据适合程度返回意向等级：\n\nA：非常适合截流，建议采集评论\nB：适合截流，可以采集评论\nC：相关但价值一般\nD：不适合截流\n\n输出要求：\n1. 只输出 JSON 数组；\n2. 每项必须包含 id、intent、reason；\n3. 不要输出 score；\n4. reason 限制 20 字以内；\n5. 不要输出 markdown，不要输出数组外文本。","expand":true},"type":"llmNode","position":{"x":420,"y":120}}]}',
       'Facebook AI群帖评论截流：同行广告/推广帖筛选默认流程',
       0,
       NOW(),
       NOW(),
       b'0',
       '1',
       '1',
       1
WHERE NOT EXISTS (SELECT 1 FROM ai_workflow WHERE code = 'fb_ai_group_comment_post_filter_v1' AND deleted = 0);

INSERT INTO ai_workflow
(id, name, code, graph, remark, status, create_time, update_time, deleted, creator, updater, tenant_id)
SELECT 2073000000000000002,
       'FB AI 群帖评论询盘分析 V1',
       'fb_ai_group_comment_analyze_v1',
       '{"edges":[{"id":"edge-group-comment-start-llm","source":"start-group-comment","target":"llm-group-comment-analyze"}],"nodes":[{"id":"start-group-comment","data":{"title":"开始","parameters":[{"id":"exportProduct","name":"exportProduct","dataType":"String","required":true,"description":"用户主营/出口产品"},{"id":"persona","name":"persona","dataType":"String","required":false,"description":"业务员人设","defaultValue":"Professional"},{"id":"needDm","name":"needDm","dataType":"Boolean","required":true,"description":"是否需要私信话术","defaultValue":"true"},{"id":"touchScoreThreshold","name":"touchScoreThreshold","dataType":"Number","required":true,"description":"触达评分阈值","defaultValue":"85"},{"id":"comments","name":"comments","dataType":"Array","required":true,"description":"评论批次，建议每批 50 条"}],"description":"群帖评论询盘分析输入参数"},"type":"startNode","position":{"x":80,"y":120}},{"id":"llm-group-comment-analyze","data":{"topK":50,"topP":0.9,"llmId":1,"title":"评论询盘意向分类","parameters":[{"id":"group-comment-param-exportProduct","ref":"start-group-comment.exportProduct","name":"exportProduct","refType":"ref","dataType":"String","description":"用户主营/出口产品"},{"id":"group-comment-param-persona","ref":"start-group-comment.persona","name":"persona","refType":"ref","dataType":"String","description":"业务员人设"},{"id":"group-comment-param-needDm","ref":"start-group-comment.needDm","name":"needDm","refType":"ref","dataType":"Boolean","description":"是否需要私信话术"},{"id":"group-comment-param-touchScoreThreshold","ref":"start-group-comment.touchScoreThreshold","name":"touchScoreThreshold","refType":"ref","dataType":"Number","description":"触达评分阈值"},{"id":"group-comment-param-comments","ref":"start-group-comment.comments","name":"comments","refType":"ref","dataType":"Array","description":"评论批次，建议每批 50 条"}],"userPrompt":"exportProduct={{exportProduct}}\npersona={{persona}}\nneedDm={{needDm}}\ntouchScoreThreshold={{touchScoreThreshold}}\ncomments={{comments}}","description":"一次分析最多 50 条评论，并为达标评论生成私信话术","temperature":0.3,"systemPrompt":"你是一名 Facebook 外贸评论询盘分析专家。\n\n用户主营/出口产品：{{exportProduct}}\n\n请综合判断评论用户是否可能是潜在买家：\n评论内容\n来源帖子内容\n群组名称\n\n重点识别询价、问库存、问型号、问采购、问联系方式、表示需要供应商/批发/代理合作的评论。\n\n触达评分阈值：{{touchScoreThreshold}}\n\n意向等级：\nA：明确询盘，建议立即私信\nB：疑似询盘，推荐联系\nC：相关但意向弱\nD：无价值，不建议联系\n\n输出要求：\n1. 只输出 JSON 数组；\n2. 每项必须包含 id、intent、reason；\n3. 不要输出 score；\n4. reason 限制 20 字以内；\n5. needDm=true 且 A/B/C 达到阈值时可输出 dm_message；\n6. dm_message 简短自然，不要提到AI分析；\n7. 不要输出 markdown，不要输出数组外文本。","expand":true},"type":"llmNode","position":{"x":420,"y":120}}]}',
       'Facebook AI群帖评论截流：评论询盘识别与私信话术默认流程',
       0,
       NOW(),
       NOW(),
       b'0',
       '1',
       '1',
       1
WHERE NOT EXISTS (SELECT 1 FROM ai_workflow WHERE code = 'fb_ai_group_comment_analyze_v1' AND deleted = 0);
