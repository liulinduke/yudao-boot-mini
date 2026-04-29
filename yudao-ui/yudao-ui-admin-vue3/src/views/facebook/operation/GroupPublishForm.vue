<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="900px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="120px"
      v-loading="formLoading"
    >
      <el-form-item label="执行账号" prop="accountIds">
        <el-select
          v-model="formData.accountIds"
          multiple
          placeholder="请选择执行账号"
          style="width: 100%"
          filterable
          @change="handleAccountChange"
        >
          <el-option
            v-for="account in accounts"
            :key="account.id"
            :label="account.fbAccount + (account.remark ? '(' + account.remark + ')' : '')"
            :value="account.id"
          />
        </el-select>
      </el-form-item>
      
      <el-form-item label="帖子内容" prop="postContent">
        <el-input
          v-model="formData.postContent"
          type="textarea"
          :rows="5"
          placeholder="请输入帖子内容"
          maxlength="5000"
          show-word-limit
        />
      </el-form-item>
      
      <el-form-item label="图片/视频" prop="mediaUrls">
        <div>
          <el-button type="primary" @click="handleSelectFiles">
            <el-icon><Plus /></el-icon>
            选择图片/视频
          </el-button>
          <el-button @click="clearFiles" v-if="formData.mediaUrls.length > 0">
            清空
          </el-button>
        </div>
        <div v-if="formData.mediaUrls.length > 0" class="mt-2">
          <el-tag
            v-for="(path, index) in formData.mediaUrls"
            :key="index"
            closable
            @close="removeFile(index)"
            class="mr-2 mb-2"
          >
            {{ getFileName(path) }}
          </el-tag>
        </div>
        <div class="text-sm text-gray-500 mt-1">最多选择10个图片或视频（本地路径）</div>
      </el-form-item>
      
      <el-form-item label="匿名发帖" prop="anonymouslyPost">
        <el-switch
          v-model="formData.anonymouslyPost"
          active-text="开启"
          inactive-text="关闭"
        />
        <div class="text-sm text-gray-500 mt-1">仅在支持的群组中可用</div>
      </el-form-item>
      
      <el-form-item label="群组类型" prop="groupType">
        <el-radio-group v-model="formData.groupType" @change="handleGroupTypeChange">
          <el-radio :label="1">已加入群组</el-radio>
          <el-radio :label="2">未加入群组</el-radio>
        </el-radio-group>
      </el-form-item>
      
      <!-- 已加入群组配置 -->
      <template v-if="formData.groupType === 1">
        <el-form-item label="每组账号数" prop="groupsPerAccount">
          <el-input-number
            v-model="formData.groupsPerAccount"
            :min="1"
            :max="50"
            placeholder="每个账号发布的群组数量"
          />
          <div class="text-sm text-gray-500 mt-1">每个账号将发布到指定数量的已加入群组</div>
        </el-form-item>
        
        <el-form-item label="选择群组">
          <el-button type="primary" @click="openGroupSelector" :disabled="formData.accountIds.length === 0">
            <Icon icon="ep:search" class="mr-5px" /> 选择群组
          </el-button>
          <div class="text-sm text-gray-500 mt-1">根据账号和数量查询可用群组</div>
        </el-form-item>
        
        <!-- 已选群组展示 -->
        <el-form-item v-if="formData.selectedGroups && formData.selectedGroups.length > 0" label="已选群组">
          <div class="w-full">
            <el-tag
              v-for="group in formData.selectedGroups.slice(0, 10)"
              :key="group.groupId"
              closable
              @close="removeSelectedGroup(group.groupId)"
              class="mr-5px mb-5px"
              size="small"
            >
              {{ group.groupName }}
            </el-tag>
            <span v-if="formData.selectedGroups.length > 10" class="text-sm text-gray-500">
              等{{ formData.selectedGroups.length }}个群组
            </span>
          </div>
        </el-form-item>
      </template>
      
      <!-- 未加入群组配置 -->
      <template v-else>
        <el-form-item label="选择群组">
          <el-button type="primary" @click="openUnjoinedGroupSelector">
            <Icon icon="ep:search" class="mr-5px" /> 选择未加入的群组
          </el-button>
          <div class="text-sm text-gray-500 mt-1">从采集的群组中选择要发布的群组</div>
        </el-form-item>
        
        <!-- 已选群组展示 -->
        <el-form-item v-if="formData.selectedUnjoinedGroups && formData.selectedUnjoinedGroups.length > 0" label="已选群组">
          <div class="w-full">
            <el-tag
              v-for="group in formData.selectedUnjoinedGroups.slice(0, 10)"
              :key="group.id"
              closable
              @close="removeUnjoinedGroup(group.id)"
              class="mr-5px mb-5px"
              size="small"
            >
              {{ group.groupName }}
            </el-tag>
            <span v-if="formData.selectedUnjoinedGroups.length > 10" class="text-sm text-gray-500">
              等{{ formData.selectedUnjoinedGroups.length }}个群组
            </span>
          </div>
        </el-form-item>
      </template>
      
      <el-form-item label="发帖间隔(秒)">
        <el-space>
          <el-input-number
            v-model="formData.minIntervalSeconds"
            :min="5"
            :max="60"
            placeholder="最小间隔"
          />
          <span>至</span>
          <el-input-number
            v-model="formData.maxIntervalSeconds"
            :min="10"
            :max="120"
            placeholder="最大间隔"
          />
        </el-space>
        <div class="text-sm text-gray-500 mt-1">每个群组发帖之间的随机间隔时间（防风控）</div>
      </el-form-item>
    </el-form>
    
    <template #footer>
      <el-button @click="dialogVisible = false">取 消</el-button>
      <el-button type="primary" @click="submitForm" :loading="formLoading">确 定</el-button>
    </template>
  </Dialog>
  
  <!-- 群组选择器弹窗（已加入群组） -->
  <GroupPublishGroupSelector
    v-model="groupSelectorVisible"
    :selected-group-ids="selectedGroupIds"
    :account-ids="formData.accountIds"
    :groups-per-account="formData.groupsPerAccount"
    @confirm="handleGroupConfirm"
  />
  
  <!-- 群组选择器弹窗（未加入群组） -->
  <GroupSelector
    v-model="unjoinedGroupSelectorVisible"
    :multiple="true"
    @confirm="handleUnjoinedGroupConfirm"
  />
</template>

<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { Plus } from '@element-plus/icons-vue'
import { Dialog } from '@/components/Dialog'
import GroupPublishGroupSelector from './GroupPublishGroupSelector.vue'
import GroupSelector from '../collect/components/GroupSelector.vue'
import * as OperationApi from '@/api/facebook/operation'
import { FbAccountApi } from '@/api/facebook/account'
import type { FbCollectGroup } from '@/api/facebook/fbcollectgroup'

defineOptions({ name: 'GroupPublishForm' })

const message = useMessage()
const { t } = useI18n()

const dialogVisible = ref(false)
const dialogTitle = ref('发群帖')
const formLoading = ref(false)
const accounts = ref<any[]>([])
const groupSelectorVisible = ref(false)
const unjoinedGroupSelectorVisible = ref(false)

// 计算已选群组的ID列表
const selectedGroupIds = computed(() => {
  return formData.value.selectedGroups.map(g => g.groupId)
})

const formData = ref({
  accountIds: [] as number[],
  postContent: '',
  mediaUrls: [] as string[],  // 存储本地文件路径
  anonymouslyPost: false,
  groupType: 1, // 1=已加入群组, 2=未加入群组
  groupsPerAccount: 5, // 每个账号发布的群组数量
  selectedGroups: [] as any[], // 已选择的群组列表（已加入）
  selectedUnjoinedGroups: [] as FbCollectGroup[], // 已选择的群组列表（未加入）
  groupKeywords: '',
  minIntervalSeconds: 10,
  maxIntervalSeconds: 20
})

const formRules = reactive({
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }],
  postContent: [{ required: true, message: '请输入帖子内容', trigger: 'blur' }]
})

const formRef = ref()

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
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 1000 })
    accounts.value = data.list || []
  } catch (error) {
    console.error('加载账号失败:', error)
  }
}

/** 选择文件 - 调用 WPF 文件选择对话框 */
const handleSelectFiles = async () => {
  try {
    // 检查是否在 WPF 环境中
    if (!window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      message.warning('请在 WPF 应用中使用此功能')
      return
    }
    
    // 调用 WPF 文件选择对话框
    const result = window.chrome.webview.hostObjects.sync.wpfBridge.SelectMediaFiles()
    const selectedFiles = JSON.parse(result) as string[]
    
    if (selectedFiles.length === 0) {
      return
    }
    
    // 限制最多10个文件
    if (selectedFiles.length > 10) {
      message.warning('最多只能选择10个文件')
      formData.value.mediaUrls = selectedFiles.slice(0, 10)
    } else {
      formData.value.mediaUrls = selectedFiles
    }
    
    message.success(`已选择 ${formData.value.mediaUrls.length} 个文件`)
    console.log('✅ 选择的文件:', formData.value.mediaUrls)
  } catch (error) {
    console.error('❌ 文件选择失败:', error)
    message.error('文件选择失败')
  }
}

/** 获取文件名 */
const getFileName = (path: string) => {
  return path.split('\\').pop() || path.split('/').pop() || path
}

/** 移除文件 */
const removeFile = (index: number) => {
  formData.value.mediaUrls.splice(index, 1)
}

/** 清空文件 */
const clearFiles = () => {
  formData.value.mediaUrls = []
}

/** 账号变化时清空群组选择 */
const handleAccountChange = () => {
  formData.value.selectedGroups = []
}

/** 群组类型变化时清空选择 */
const handleGroupTypeChange = () => {
  formData.value.selectedGroups = []
  formData.value.selectedUnjoinedGroups = []
}

/** 打开群组选择器（已加入） */
const openGroupSelector = () => {
  if (formData.value.accountIds.length === 0) {
    message.warning('请先选择执行账号')
    return
  }
  groupSelectorVisible.value = true
}

/** 打开群组选择器（未加入） */
const openUnjoinedGroupSelector = () => {
  unjoinedGroupSelectorVisible.value = true
}

/** 群组选择确认（已加入） */
const handleGroupConfirm = (groups: any[]) => {
  formData.value.selectedGroups = groups
}

/** 群组选择确认（未加入） */
const handleUnjoinedGroupConfirm = (groups: FbCollectGroup[]) => {
  formData.value.selectedUnjoinedGroups = groups
}

/** 移除已选群组（已加入） */
const removeSelectedGroup = (groupId: string) => {
  formData.value.selectedGroups = formData.value.selectedGroups.filter(g => g.groupId !== groupId)
}

/** 移除已选群组（未加入） */
const removeUnjoinedGroup = (groupId: string) => {
  formData.value.selectedUnjoinedGroups = formData.value.selectedUnjoinedGroups.filter(g => g.id !== groupId)
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    accountIds: [],
    postContent: '',
    mediaUrls: [],
    anonymouslyPost: false,
    groupType: 1, // 1=已加入群组, 2=未加入群组
    groupsPerAccount: 5, // 每个账号发布的群组数量
    selectedGroups: [], // 已选择的群组列表（已加入）
    selectedUnjoinedGroups: [], // 已选择的群组列表（未加入）
    groupKeywords: '',
    minIntervalSeconds: 10,
    maxIntervalSeconds: 20
  }
  formRef.value?.resetFields()
}

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  const valid = await formRef.value?.validate()
  if (!valid) return
  
  try {
    formLoading.value = true
    
    // 计算期望数量
    let expectedCount = 0
    if (formData.value.groupType === 1) {
      // 已加入群组：账号数 × 每组账号数
      expectedCount = formData.value.accountIds.length * formData.value.groupsPerAccount
    } else {
      // 未加入群组：根据选择的群组数量
      expectedCount = formData.value.selectedUnjoinedGroups.length
    }
    
    // 构建任务数据
    const data = {
      taskType: 13, // 发群帖
      taskName: `发群帖-${new Date().getTime()}`, // 自动生成任务名称
      accountIds: formData.value.accountIds,
      expectedCount: expectedCount,
      actionConfig: JSON.stringify({
        postContent: formData.value.postContent,
        mediaUrls: formData.value.mediaUrls,
        anonymouslyPost: formData.value.anonymouslyPost,
        groupType: formData.value.groupType,
        groupsPerAccount: formData.value.groupsPerAccount,
        selectedGroups: formData.value.selectedGroups,
        selectedUnjoinedGroups: formData.value.selectedUnjoinedGroups.map(g => ({
          groupId: g.id,
          groupName: g.groupName,
          groupUrl: g.url
        })),
        minIntervalSeconds: formData.value.minIntervalSeconds,
        maxIntervalSeconds: formData.value.maxIntervalSeconds
      })
    }
    
    // 1. 创建任务
    const taskId = await OperationApi.createFbOperationTask(data)
    console.log('✅ 发群帖任务创建成功, TaskId:', taskId)
    
    // 2. 调用 WPF 执行任务（为每个账号启动）
    // @ts-ignore
    if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      console.log('🚀 开始调用 WPF 执行发群帖任务...')
      
      // 获取账号信息（需要从后端获取cookie）
      for (const accountId of formData.value.accountIds) {
        try {
          // TODO: 这里需要从后端获取账号的 cookie
          // 暂时使用空字符串，实际使用时需要从 FbAccountApi 获取
          const accountInfo = await FbAccountApi.getFbAccount(accountId)
          const cookie = accountInfo.cookie || ''
          
          console.log(`👥 启动账号 ${accountId} 的发群帖任务`)
          
          // @ts-ignore
          window.chrome.webview.hostObjects.sync.wpfBridge.StartGroupPublishTask(
            String(taskId),
            String(accountId),
            cookie,
            data.actionConfig
          )
          
          // 等待间隔时间（防风控）
          const intervalSeconds = (formData.value.minIntervalSeconds + formData.value.maxIntervalSeconds) / 2
          await new Promise(resolve => setTimeout(resolve, intervalSeconds * 1000))
        } catch (error) {
          console.error(`❌ 启动账号 ${accountId} 的任务失败:`, error)
        }
      }
      
      message.success('任务已创建并发送到 WPF 队列')
    } else {
      console.warn('⚠️ WPF 桥接对象不存在，任务已创建但未执行')
      message.warning('任务已创建，但 WPF 未连接')
    }
    
    dialogVisible.value = false
    emit('success')
  } catch (error) {
    console.error('创建任务失败:', error)
    message.error('创建失败')
  } finally {
    formLoading.value = false
  }
}
</script>

<style scoped lang="scss">
.text-sm {
  font-size: 12px;
}
.text-gray-500 {
  color: #9ca3af;
}
.mt-1 {
  margin-top: 4px;
}
</style>
