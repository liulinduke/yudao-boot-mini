<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="900px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="140px"
      v-loading="formLoading"
    >
      <!-- 风控警告 -->
      <div style="margin-bottom: 12px">
        <el-alert
          title="建议每个账号每日转帖操作不超过10次，避免触发风控机制"
          type="warning"
          :closable="false"
          show-icon
        />
      </div>

      <!-- 帖子链接 -->
      <el-form-item label="帖子链接" prop="postUrl">
        <el-input
          v-model="formData.postUrl"
          placeholder="请输入Facebook帖子链接"
          type="textarea"
          :rows="2"
        />
      </el-form-item>

      <!-- 执行账号 -->
      <el-form-item
        label="执行账号"
        prop="accountIds"
        :rules="formData.accountSelectionMode === 'MANUAL' ? formRules.accountIds : []"
      >
        <FbAccountSelector v-model="formData.accountIds" v-model:selection-mode="formData.accountSelectionMode" :action-types="['repost']" class="w-full" />
      </el-form-item>

      <!-- 执行项配置 -->
      <el-form-item label="执行项" prop="actionConfig">
        <div class="w-full">
          <!-- 执行项：点赞 -->
          <div class="mb-10px flex items-center">
            <el-checkbox v-model="selectedActions" :label="1" class="mr-20px">点赞</el-checkbox>
          </div>

          <!-- 执行项：转发到动态消息 -->
          <div class="mb-10px flex items-center">
            <el-checkbox v-model="selectedActions" :label="2">转发到动态消息</el-checkbox>
          </div>

          <!-- 动态附言 -->
          <template v-if="selectedActions.includes(2)">
            <el-form-item label="动态附言" class="!mb-10px" label-width="140px">
              <div class="w-full">
                <el-radio-group v-model="feedScriptType" class="mb-2">
                  <el-radio :label="1">手动输入</el-radio>
                  <el-radio :label="2">从话术库选择</el-radio>
                </el-radio-group>

                <!-- 手动输入模式 -->
                <el-input
                  v-if="feedScriptType === 1"
                  v-model="feedManualScripts"
                  type="textarea"
                  :rows="3"
                  placeholder="Share now 时附带的文字（可选）"
                />

                <!-- 话术库模式 -->
                <div v-else>
                  <el-button type="primary" @click="openFeedScriptSelector" class="mb-2">
                    <Icon icon="ep:plus" class="mr-5px" /> 从话术库选择
                  </el-button>
                  <div v-if="feedSelectedScripts.length > 0" class="mt-2">
                    <el-tag
                      v-for="(script, index) in feedSelectedScripts.slice(0, 5)"
                      :key="index"
                      closable
                      @close="removeFeedScript(index)"
                      class="mr-2 mb-2"
                      size="small"
                    >
                      {{ script.substring(0, 30) }}...
                    </el-tag>
                    <span v-if="feedSelectedScripts.length > 5" class="text-sm text-gray-500">
                      等{{ feedSelectedScripts.length }}条话术
                    </span>
                  </div>
                </div>

                <!-- 随机表情 -->
                <el-checkbox v-model="feedAppendRandomEmoji" class="mt-2">
                  追加随机 Facebook 表情（每条话术末尾随机 1~2 个）
                </el-checkbox>
              </div>
            </el-form-item>
          </template>

          <!-- 执行项：评论 -->
          <div class="mb-10px flex items-center">
            <el-checkbox v-model="selectedActions" :label="6">评论</el-checkbox>
          </div>

          <!-- 评论话术配置 -->
          <template v-if="selectedActions.includes(6)">
            <el-form-item label="评论话术" class="!mb-10px" label-width="140px">
              <div class="w-full">
                <el-radio-group v-model="commentScriptType" class="mb-2">
                  <el-radio :label="1">手动输入</el-radio>
                  <el-radio :label="2">从话术库选择</el-radio>
                </el-radio-group>

                <!-- 手动输入模式 -->
                <el-input
                  v-if="commentScriptType === 1"
                  v-model="commentManualScripts"
                  type="textarea"
                  :rows="3"
                  placeholder="请输入评论话术"
                />

                <!-- 话术库模式 -->
                <div v-else>
                  <el-button type="primary" @click="openCommentScriptSelector" class="mb-2">
                    <Icon icon="ep:plus" class="mr-5px" /> 从话术库选择
                  </el-button>
                  <div v-if="commentSelectedScripts.length > 0" class="mt-2">
                    <el-tag
                      v-for="(script, index) in commentSelectedScripts.slice(0, 5)"
                      :key="index"
                      closable
                      @close="removeCommentScript(index)"
                      class="mr-2 mb-2"
                      size="small"
                    >
                      {{ script.substring(0, 30) }}...
                    </el-tag>
                    <span v-if="commentSelectedScripts.length > 5" class="text-sm text-gray-500">
                      等{{ commentSelectedScripts.length }}条话术
                    </span>
                  </div>
                </div>

                <!-- 随机表情 -->
                <el-checkbox v-model="commentAppendRandomEmoji" class="mt-2">
                  追加随机 Facebook 表情（每条话术末尾随机 1~2 个）
                </el-checkbox>
              </div>
            </el-form-item>
          </template>

          <!-- 第二行：转贴到好友 + 数量 -->
          <div class="mb-10px">
            <el-checkbox v-model="selectedActions" :label="4"> 转贴到好友 </el-checkbox>
            <el-input-number
              v-if="selectedActions.includes(4)"
              v-model="actionConfig.shareToFriendCount"
              :min="1"
              :max="100"
              size="small"
              class="ml-10px"
            />
          </div>

          <!-- 好友附言 -->
          <template v-if="selectedActions.includes(4)">
            <el-form-item label="好友附言" class="!mb-10px" label-width="140px">
              <div class="w-full">
                <el-radio-group v-model="friendScriptType" class="mb-2">
                  <el-radio :label="1">手动输入</el-radio>
                  <el-radio :label="2">从话术库选择</el-radio>
                </el-radio-group>

                <!-- 手动输入模式 -->
                <el-input
                  v-if="friendScriptType === 1"
                  v-model="friendManualScripts"
                  type="textarea"
                  :rows="3"
                  placeholder="Messenger 发送给好友时的附言（可选）"
                />

                <!-- 话术库模式 -->
                <div v-else>
                  <el-button type="primary" @click="openFriendScriptSelector" class="mb-2">
                    <Icon icon="ep:plus" class="mr-5px" /> 从话术库选择
                  </el-button>
                  <div v-if="friendSelectedScripts.length > 0" class="mt-2">
                    <el-tag
                      v-for="(script, index) in friendSelectedScripts.slice(0, 5)"
                      :key="index"
                      closable
                      @close="removeFriendScript(index)"
                      class="mr-2 mb-2"
                      size="small"
                    >
                      {{ script.substring(0, 30) }}...
                    </el-tag>
                    <span v-if="friendSelectedScripts.length > 5" class="text-sm text-gray-500">
                      等{{ friendSelectedScripts.length }}条话术
                    </span>
                  </div>
                </div>

                <!-- 随机表情 -->
                <el-checkbox v-model="friendAppendRandomEmoji" class="mt-2">
                  追加随机 Facebook 表情（每条话术末尾随机 1~2 个）
                </el-checkbox>
              </div>
            </el-form-item>
          </template>

          <!-- 转发到群组 + 数量 + 选择按钮 -->
          <div class="mb-10px">
            <div class="flex items-center">
              <el-checkbox v-model="selectedActions" :label="5"> 转发到群组 </el-checkbox>
              <el-input-number
                v-if="selectedActions.includes(5)"
                v-model="actionConfig.shareToGroupCount"
                :min="1"
                :max="100"
                size="small"
                class="ml-10px"
              />
              <el-button
                v-if="selectedActions.includes(5)"
                type="primary"
                size="small"
                class="ml-10px"
                @click="openGroupSelector"
              >
                选择群组
              </el-button>
            </div>
          </div>

          <!-- 已选群组展示 -->
          <div v-if="selectedGroups.length > 0" class="mt-10px">
            <div class="text-sm text-gray-600 mb-5px"
              >已选择 {{ selectedGroups.length }} 个群组：</div
            >
            <el-tag
              v-for="group in selectedGroups.slice(0, 10)"
              :key="group.groupId"
              closable
              @close="removeSelectedGroup(group.groupId)"
              class="mr-5px mb-5px"
              size="small"
            >
              {{ group.groupName }}
            </el-tag>
            <span v-if="selectedGroups.length > 10" class="text-sm text-gray-500">
              等{{ selectedGroups.length }}个群组
            </span>
          </div>

          <!-- 群组附言 -->
          <template v-if="selectedActions.includes(5)">
            <el-form-item label="群组附言" class="!mb-10px" label-width="140px">
              <div class="w-full">
                <el-radio-group v-model="groupScriptType" class="mb-2">
                  <el-radio :label="1">手动输入</el-radio>
                  <el-radio :label="2">从话术库选择</el-radio>
                </el-radio-group>

                <!-- 手动输入模式 -->
                <el-input
                  v-if="groupScriptType === 1"
                  v-model="groupManualScripts"
                  type="textarea"
                  :rows="3"
                  placeholder="转发到群组时的附言（可选）"
                />

                <!-- 话术库模式 -->
                <div v-else>
                  <el-button type="primary" @click="openGroupScriptSelector" class="mb-2">
                    <Icon icon="ep:plus" class="mr-5px" /> 从话术库选择
                  </el-button>
                  <div v-if="groupSelectedScripts.length > 0" class="mt-2">
                    <el-tag
                      v-for="(script, index) in groupSelectedScripts.slice(0, 5)"
                      :key="index"
                      closable
                      @close="removeGroupScript(index)"
                      class="mr-2 mb-2"
                      size="small"
                    >
                      {{ script.substring(0, 30) }}...
                    </el-tag>
                    <span v-if="groupSelectedScripts.length > 5" class="text-sm text-gray-500">
                      等{{ groupSelectedScripts.length }}条话术
                    </span>
                  </div>
                </div>

                <!-- 随机表情 -->
                <el-checkbox v-model="groupAppendRandomEmoji" class="mt-2">
                  追加随机 Facebook 表情（每条话术末尾随机 1~2 个）
                </el-checkbox>
              </div>
            </el-form-item>
          </template>
        </div>
      </el-form-item>

      <!-- 备注 -->
      <el-form-item label="备注" prop="remark">
        <el-input v-model="formData.remark" type="textarea" placeholder="请输入备注" />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button type="primary" @click="submitForm" :loading="formLoading">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>

  <!-- 群组选择器 -->
  <GroupSelectorForRepost
    v-model="groupSelectorVisible"
    :selected-group-ids="selectedGroupIds"
    :account-ids="formData.accountIds"
    @confirm="handleGroupConfirm"
  />

  <!-- 评论话术选择器 -->
  <ScriptSelector v-model="commentScriptSelectorVisible" @confirm="handleCommentScriptConfirm" />

  <!-- 动态附言话术选择器 -->
  <ScriptSelector v-model="feedScriptSelectorVisible" @confirm="handleFeedScriptConfirm" />

  <!-- 好友附言话术选择器 -->
  <ScriptSelector v-model="friendScriptSelectorVisible" @confirm="handleFriendScriptConfirm" />

  <!-- 群组附言话术选择器 -->
  <ScriptSelector v-model="groupScriptSelectorVisible" @confirm="handleGroupScriptConfirm" />
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbAccountApi, filterSelectableFbAccounts } from '@/api/facebook/account'
import FbAccountSelector from '../components/FbAccountSelector.vue'
import {
  createFbOperationTask,
  FbOperationTaskSaveReqVO
} from '@/api/facebook/operation'
import GroupSelectorForRepost from './GroupSelectorForRepost.vue'
import ScriptSelector from './dmtask/ScriptSelector.vue'

const message = useMessage()
const { t } = useI18n()

const dialogVisible = ref(false)
const dialogTitle = ref('创建转帖任务')
const formLoading = ref(false)
const formRef = ref()

// 表单数据
const formData = ref({
  postUrl: '',
  accountIds: [] as string[],
  accountSelectionMode: 'AUTO' as 'AUTO' | 'MANUAL',
  remark: ''
})

// 执行项配置
const selectedActions = ref<number[]>([])
const actionConfig = ref({
  shareToFriendCount: 10,
  shareToGroupCount: 1
})

// 群组选择
const selectedGroups = ref<any[]>([])
const groupSelectorVisible = ref(false)

// 账号列表
const accounts = ref<any[]>([])

// 评论话术相关
const commentScriptType = ref(1)
const commentManualScripts = ref('')
const commentSelectedScripts = ref<string[]>([])
const commentAppendRandomEmoji = ref(false)
const commentScriptSelectorVisible = ref(false)

// 动态附言话术相关
const feedScriptType = ref(1)
const feedManualScripts = ref('')
const feedSelectedScripts = ref<string[]>([])
const feedAppendRandomEmoji = ref(false)
const feedScriptSelectorVisible = ref(false)

// 好友附言话术相关
const friendScriptType = ref(1)
const friendManualScripts = ref('')
const friendSelectedScripts = ref<string[]>([])
const friendAppendRandomEmoji = ref(false)
const friendScriptSelectorVisible = ref(false)

// 群组附言话术相关
const groupScriptType = ref(1)
const groupManualScripts = ref('')
const groupSelectedScripts = ref<string[]>([])
const groupAppendRandomEmoji = ref(false)
const groupScriptSelectorVisible = ref(false)

// 表单验证规则
const formRules = reactive({
  postUrl: [
    { required: true, message: '请输入帖子链接', trigger: 'blur' },
    { pattern: /^https?:\/\/.*/, message: '请输入有效的URL', trigger: 'blur' }
  ],
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }]
})

// 计算已选群组ID列表
const selectedGroupIds = computed(() => {
  return selectedGroups.value.map((g) => g.groupId)
})

/** 打开弹窗 */
const open = async () => {
  dialogVisible.value = true
  resetForm()
  await loadAccounts()
}

defineExpose({ open })

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
    if (data && data.list && Array.isArray(data.list)) {
      accounts.value = filterSelectableFbAccounts(data.list)
    } else {
      accounts.value = []
    }
  } catch (error) {
    console.error('加载账号列表失败:', error)
    accounts.value = []
  }
}

/** 打开群组选择器 */
const openGroupSelector = () => {
  groupSelectorVisible.value = true
}

/** 移除已选群组 */
const removeSelectedGroup = (groupId: string) => {
  const index = selectedGroups.value.findIndex((g) => g.groupId === groupId)
  if (index > -1) {
    selectedGroups.value.splice(index, 1)
  }
}

/** 确认群组选择 */
const handleGroupConfirm = (groups: any[]) => {
  selectedGroups.value = groups
  message.success(`已选择 ${groups.length} 个群组`)
}

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  if (!formRef.value) return

  // 验证执行项
  if (selectedActions.value.length === 0) {
    message.warning('请至少选择一个执行项')
    return
  }

  // 验证转发到群组的群组选择
  if (selectedActions.value.includes(5) && selectedGroups.value.length === 0) {
    message.warning('请选择要转发到的群组')
    return
  }

  await formRef.value.validate()

  formLoading.value = true
  try {
    // 生成任务名称
    const timestamp = new Date()
      .toLocaleString('zh-CN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      })
      .replace(/[\/\s:]/g, '')

    // 获取话术列表（手动输入为单条，话术库选择为多条随机）
    const getScripts = (manual: string, selected: string[]) => {
      if (manual.trim()) {
        return [manual.trim()]
      }
      return selected
    }

    // 构建执行项配置JSON
    const configData = {
      actions: selectedActions.value.filter((a) => a !== 3),
      // 评论话术配置
      commentScripts: getScripts(commentManualScripts.value, commentSelectedScripts.value),
      commentAppendRandomEmoji: commentAppendRandomEmoji.value,
      // 动态附言配置
      feedScripts: getScripts(feedManualScripts.value, feedSelectedScripts.value),
      feedAppendRandomEmoji: feedAppendRandomEmoji.value,
      // 好友附言配置
      friendScripts: getScripts(friendManualScripts.value, friendSelectedScripts.value),
      friendAppendRandomEmoji: friendAppendRandomEmoji.value,
      // 群组附言配置
      groupScripts: getScripts(groupManualScripts.value, groupSelectedScripts.value),
      groupAppendRandomEmoji: groupAppendRandomEmoji.value,
      // 其他配置
      shareToFriendCount: actionConfig.value.shareToFriendCount,
      shareToGroupCount: actionConfig.value.shareToGroupCount,
      selectedGroups: selectedGroups.value
    }

    // 计算期望数量
    let expectedCount = 0
    if (selectedActions.value.includes(1)) expectedCount += formData.value.accountIds.length // 点赞：每个账号1次
    if (selectedActions.value.includes(2)) expectedCount += formData.value.accountIds.length // 转发到动态消息
    if (selectedActions.value.includes(6)) expectedCount += formData.value.accountIds.length // 评论：每个账号1次
    if (selectedActions.value.includes(4))
      expectedCount += actionConfig.value.shareToFriendCount * formData.value.accountIds.length // 转贴到好友：可配置次数
    if (selectedActions.value.includes(5))
      expectedCount += selectedGroups.value.length * formData.value.accountIds.length // 转发到群组：按选择的群组数量

    // 根据话术类型设置参数
    const submitData = {
      taskType: 10, // 转贴任务（运营任务从10开始，避免与采集任务冲突）
      taskName: `转帖_${timestamp}`,
      accountIds: formData.value.accountIds,
      accountSelectionMode: formData.value.accountSelectionMode,
      postUrl: formData.value.postUrl,
      actionConfig: JSON.stringify(configData),
      expectedCount: expectedCount,
      remark: formData.value.remark
    } as unknown as FbOperationTaskSaveReqVO

    await createFbOperationTask(submitData)
    message.success('转帖任务已创建，已加入账号串行队列')

    dialogVisible.value = false
    emit('success')
  } finally {
    formLoading.value = false
  }
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    postUrl: '',
    accountIds: [],
    accountSelectionMode: 'AUTO',
    remark: ''
  }
  selectedActions.value = []
  actionConfig.value = {
    shareToFriendCount: 10,
    shareToGroupCount: 1
  }
  selectedGroups.value = []

  // 重置评论话术
  commentScriptType.value = 1
  commentManualScripts.value = ''
  commentSelectedScripts.value = []
  commentAppendRandomEmoji.value = false

  // 重置动态附言话术
  feedScriptType.value = 1
  feedManualScripts.value = ''
  feedSelectedScripts.value = []
  feedAppendRandomEmoji.value = false

  // 重置好友附言话术
  friendScriptType.value = 1
  friendManualScripts.value = ''
  friendSelectedScripts.value = []
  friendAppendRandomEmoji.value = false

  // 重置群组附言话术
  groupScriptType.value = 1
  groupManualScripts.value = ''
  groupSelectedScripts.value = []
  groupAppendRandomEmoji.value = false

  formRef.value?.resetFields()
}

/** 评论话术选择器相关方法 */
const openCommentScriptSelector = () => {
  commentScriptSelectorVisible.value = true
}

const handleCommentScriptConfirm = (scripts: any[] | any) => {
  const list = Array.isArray(scripts) ? scripts : [scripts]
  for (const script of list) {
    const content = script?.scriptContent?.trim()
    if (content && !commentSelectedScripts.value.includes(content)) {
      commentSelectedScripts.value.push(content)
    }
  }
}

const removeCommentScript = (index: number) => {
  commentSelectedScripts.value.splice(index, 1)
}

/** 动态附言话术选择器相关方法 */
const openFeedScriptSelector = () => {
  feedScriptSelectorVisible.value = true
}

const handleFeedScriptConfirm = (scripts: any[] | any) => {
  const list = Array.isArray(scripts) ? scripts : [scripts]
  for (const script of list) {
    const content = script?.scriptContent?.trim()
    if (content && !feedSelectedScripts.value.includes(content)) {
      feedSelectedScripts.value.push(content)
    }
  }
}

const removeFeedScript = (index: number) => {
  feedSelectedScripts.value.splice(index, 1)
}

/** 好友附言话术选择器相关方法 */
const openFriendScriptSelector = () => {
  friendScriptSelectorVisible.value = true
}

const handleFriendScriptConfirm = (scripts: any[] | any) => {
  const list = Array.isArray(scripts) ? scripts : [scripts]
  for (const script of list) {
    const content = script?.scriptContent?.trim()
    if (content && !friendSelectedScripts.value.includes(content)) {
      friendSelectedScripts.value.push(content)
    }
  }
}

const removeFriendScript = (index: number) => {
  friendSelectedScripts.value.splice(index, 1)
}

/** 群组附言话术选择器相关方法 */
const openGroupScriptSelector = () => {
  groupScriptSelectorVisible.value = true
}

const handleGroupScriptConfirm = (scripts: any[] | any) => {
  const list = Array.isArray(scripts) ? scripts : [scripts]
  for (const script of list) {
    const content = script?.scriptContent?.trim()
    if (content && !groupSelectedScripts.value.includes(content)) {
      groupSelectedScripts.value.push(content)
    }
  }
}

const removeGroupScript = (index: number) => {
  groupSelectedScripts.value.splice(index, 1)
}
</script>

<style scoped lang="scss">
:deep(.el-checkbox) {
  margin-right: 20px;
  margin-bottom: 10px;
}
</style>
