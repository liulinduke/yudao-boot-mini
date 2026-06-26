package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.List;

@Schema(description = "管理后台 - Facebook AI获客Agent关键词生成 Response VO")
@Data
@NoArgsConstructor
@AllArgsConstructor
public class FbAiKeywordGenerateRespVO {

    @Schema(description = "关键词列表")
    private List<String> keywords;

}
