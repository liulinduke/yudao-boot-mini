package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;

import lombok.Data;

import java.util.ArrayList;
import java.util.List;

@Data
public class AiSearchSnapshotVO {
    private Integer version = 1;
    private List<String> selectedKeywords = new ArrayList<>();
    private List<String> productTerms = new ArrayList<>();
    private List<String> customerRoleTerms = new ArrayList<>();
    private List<String> purchasingTerms = new ArrayList<>();
    private List<String> localLanguageTerms = new ArrayList<>();
    private List<String> applications = new ArrayList<>();
    private List<String> scenarios = new ArrayList<>();
    private List<String> ecommerceChannelTerms = new ArrayList<>();
    private List<String> upstreamDownstreamTerms = new ArrayList<>();
}
