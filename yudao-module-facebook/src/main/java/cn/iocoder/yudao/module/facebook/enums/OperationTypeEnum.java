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

    DM("dm", "私信", 100),
    REPOST("repost", "转帖", 50),
    JOIN_GROUP("join_group", "加组", 30),
    COMMENT("comment", "评论", 200);

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
