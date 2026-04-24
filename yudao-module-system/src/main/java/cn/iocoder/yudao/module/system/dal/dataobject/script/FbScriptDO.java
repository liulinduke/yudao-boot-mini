package cn.iocoder.yudao.module.system.dal.dataobject.script;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * 话术内容 DO
 *
 * @author 芋道源码
 */
@TableName("social_matrix_script")
@KeySequence("social_matrix_script_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbScriptDO extends TenantBaseDO {

    /**
     * 话术ID
     */
    @TableId
    private Long id;
    /**
     * 分组ID
     */
    private Long groupId;
    /**
     * 话术标题
     */
    private String scriptTitle;
    /**
     * 话术内容
     */
    private String scriptContent;
    /**
     * 内容类型（1-文本 2-图文 3-视频 4-音频）
     */
    private Integer contentType;
    /**
     * 附件列表（JSON格式）
     */
    private String attachments;
    /**
     * 是否按顺序发送
     */
    private Boolean sendSequence;
    /**
     * 排序
     */
    private Integer sort;
    /**
     * 备注
     */
    private String remark;

}
