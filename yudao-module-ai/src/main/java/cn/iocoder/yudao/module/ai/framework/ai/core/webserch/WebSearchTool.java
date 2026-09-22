package cn.iocoder.yudao.module.ai.framework.ai.core.webserch;

/**
 * Agent 使用的联网搜索工具抽象。模型只负责决定何时调用工具，具体搜索后端由实现提供。
 */
public interface WebSearchTool {

    AiWebSearchResponse search(String query, Integer count);
}
