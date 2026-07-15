package cn.iocoder.yudao.module.facebook.dal.mysql.message;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.FbMessageConversationDO;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import org.apache.ibatis.annotations.Mapper;

@Mapper
public interface FbMessageConversationMapper extends BaseMapperX<FbMessageConversationDO> {
    default FbMessageConversationDO selectByKey(Long accountId, String conversationKey) {
        return selectOne(new LambdaQueryWrapper<FbMessageConversationDO>()
                .eq(FbMessageConversationDO::getAccountId, accountId)
                .eq(FbMessageConversationDO::getConversationKey, conversationKey));
    }
}
