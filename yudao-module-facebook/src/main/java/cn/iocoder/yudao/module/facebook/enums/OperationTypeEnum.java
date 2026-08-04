package cn.iocoder.yudao.module.facebook.enums;

import lombok.AllArgsConstructor;
import lombok.Getter;

/**
 * Facebook 操作类型枚举
 *
 * @author 芋道源码
 */
@Getter
@AllArgsConstructor
public enum OperationTypeEnum {

    DM("dm", "私信", 10),
    REPOST("repost", "转帖", 10),
    JOIN_GROUP("join_group", "加组", 10),
    GROUP_POST("group_post", "发群帖", 10),
    COMMENT("comment", "评论", 10),
    FOLLOW("follow", "关注", 10);

    /**
     * 操作类型代码
     */
    private final String code;

    /**
     * 操作类型名称
     */
    private final String name;

    /**
     * 默认每日限制次数
     */
    private final Integer defaultLimit;

    public static OperationTypeEnum getByCode(String code) {
        for (OperationTypeEnum type : values()) {
            if (type.getCode().equals(code)) {
                return type;
            }
        }
        return null;
    }

}
