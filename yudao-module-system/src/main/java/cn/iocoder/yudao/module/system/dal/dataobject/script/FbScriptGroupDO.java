package cn.iocoder.yudao.module.system.dal.dataobject.script;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * 话术分组 DO
 *
 * @author 芋道源码
 */
@TableName("social_matrix_script_group")
@KeySequence("social_matrix_script_group_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbScriptGroupDO extends TenantBaseDO {

    /**
     * 分组ID
     */
    @TableId
    private Long id;
    /**
     * 分组名称
     */
    private String groupName;
    /**
     * 分组类型（1-公共 2-私有）
     */
    private Integer groupType;
    /**
     * 排序
     */
    private Integer sort;
    /**
     * 备注
     */
    private String remark;

}
