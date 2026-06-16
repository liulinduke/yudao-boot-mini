
package cn.iocoder.yudao.module.system.dal.dataobject;

import cn.iocoder.yudao.framework.mybatis.core.dataobject.BaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * 代理信息 DO
 *
 * @author 芋道源码
 */
@TableName("sys_proxy")
@KeySequence("sys_proxy_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class SysProxyDO extends BaseDO {

    /**
     * 代理ID
     */
    @TableId
    private Long id;

    /**
     * 代理名称
     */
    private String proxyName;

    /**
     * 代理类型（1-HTTP, 2-HTTPS, 3-SOCKS5）
     */
    private Integer proxyType;

    /**
     * 代理服务器地址
     */
    private String host;

    /**
     * 代理端口
     */
    private Integer port;

    /**
     * 代理认证用户名
     */
    private String username;

    /**
     * 代理认证密码（加密存储）
     */
    private String password;

    /**
     * 国家/地区
     */
    private String country;

    /**
     * 状态（0-禁用，1-启用）
     */
    private Integer status;

    /**
     * 备注
     */
    private String remark;

}
