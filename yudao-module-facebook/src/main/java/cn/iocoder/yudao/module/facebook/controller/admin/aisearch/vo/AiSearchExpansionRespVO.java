package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import lombok.Data;
import java.util.List;
@Data
public class AiSearchExpansionRespVO {
    private List<String> keywords;
    private List<String> scenes;
    private List<String> productTerms;
    private List<String> customerRoles;
    private List<String> localTerms;
}
