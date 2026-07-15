<script lang="ts" setup>
import { formatDate } from '@/utils/formatTime'
import * as NotifyMessageApi from '@/api/system/notify/message'
import { FbMessageApi } from '@/api/facebook/message'
import { useUserStoreWithOut } from '@/store/modules/user'
import { propTypes } from '@/utils/propTypes'

defineOptions({ name: 'Message' })

defineProps({
  color: propTypes.string.def('')
})

const { push } = useRouter()
const userStore = useUserStoreWithOut()
const activeName = ref('notice')
const unreadCount = ref(0) // 未读消息数量
const list = ref<any[]>([]) // 消息列表
const facebookUnread = reactive({ messenger: 0, notification: 0 })

// 获得消息列表
const getList = async () => {
  list.value = await NotifyMessageApi.getUnreadNotifyMessageList()
}

// 获得未读消息数
const getUnreadCount = async () => {
  const [systemCount, summaries] = await Promise.all([
    NotifyMessageApi.getUnreadNotifyMessageCount(),
    FbMessageApi.getUnreadSummary().catch(() => [])
  ])
  facebookUnread.messenger = summaries.reduce((total, item) => total + Number(item.messengerUnreadCount || 0), 0)
  facebookUnread.notification = summaries.reduce((total, item) => total + Number(item.commentUnreadCount || 0), 0)
  unreadCount.value = Number(systemCount || 0) + facebookUnread.messenger + facebookUnread.notification
}

// 跳转我的站内信
const goMyList = () => {
  push({
    name: 'MyNotifyMessage'
  })
}

const openFacebookMessageManager = () => {
  const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
  if (bridge?.OpenMessageManagerWindow) {
    bridge.OpenMessageManagerWindow()
    return
  }
  push({ name: 'FacebookMessage' })
}

// ========== 初始化 =========
onMounted(() => {
  // 首次加载小红点
  getUnreadCount()
  // 轮询刷新小红点
  setInterval(
    () => {
      if (userStore.getIsSetUser) {
        getUnreadCount()
      } else {
        unreadCount.value = 0
      }
    },
    1000 * 60 * 2
  )
})
</script>
<template>
  <div class="message">
    <ElPopover :width="400" placement="bottom" trigger="click">
      <template #reference>
        <ElBadge :is-dot="unreadCount > 0" class="item">
          <Icon :size="18" class="cursor-pointer" icon="ep:bell" :color="color" @click="getList" />
        </ElBadge>
      </template>
      <ElTabs v-model="activeName">
        <ElTabPane label="我的站内信" name="notice">
          <el-scrollbar class="message-list">
            <template v-for="item in list" :key="item.id">
              <div class="message-item">
                <img alt="" class="message-icon" src="@/assets/imgs/avatar.jpg" />
                <div class="message-content">
                  <span class="message-title">
                    {{ item.templateNickname }}：{{ item.templateContent }}
                  </span>
                  <span class="message-date">
                    {{ formatDate(item.createTime) }}
                  </span>
                </div>
              </div>
            </template>
          </el-scrollbar>
        </ElTabPane>
      </ElTabs>
      <div class="facebook-summary" @click="openFacebookMessageManager">
        <Icon icon="ep:chat-dot-round" :size="20" />
        <span>Facebook</span>
        <span class="facebook-count">消息 {{ facebookUnread.messenger }}，通知 {{ facebookUnread.notification }}</span>
      </div>
      <!-- 更多 -->
      <div style="margin-top: 10px; text-align: right">
        <XButton preIcon="ep:view" title="查看全部" type="primary" @click="goMyList" />
      </div>
    </ElPopover>
  </div>
</template>
<style lang="scss" scoped>
.message-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 260px;
  line-height: 45px;
}

.message-list {
  display: flex;
  height: 400px;
  flex-direction: column;

  .message-item {
    display: flex;
    align-items: center;
    padding: 20px 0;
    border-bottom: 1px solid var(--el-border-color-light);

    &:last-child {
      border: none;
    }

    .message-icon {
      width: 40px;
      height: 40px;
      margin: 0 20px 0 5px;
    }

    .message-content {
      display: flex;
      flex-direction: column;

      .message-title {
        margin-bottom: 5px;
      }

      .message-date {
        font-size: 12px;
        color: var(--el-text-color-secondary);
      }
    }
  }
}

.facebook-summary {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 12px;
  padding: 10px;
  cursor: pointer;
  border: 1px solid var(--el-border-color-light);
  color: var(--el-text-color-primary);

  &:hover {
    background: var(--el-fill-color-light);
  }
}

.facebook-count {
  margin-left: auto;
  color: var(--el-color-primary);
  font-size: 12px;
}
</style>
