<template>
  <ContentWrap>
    <!-- 搜索工作栏 -->
    <el-form
      class="-mb-15px"
      :model="queryParams"
      ref="queryFormRef"
      :inline="true"
      label-width="68px"
    >
      <el-form-item label="话术标题" prop="scriptTitle">
        <el-input
          v-model="queryParams.scriptTitle"
          placeholder="请输入话术标题"
          clearable
          @keyup.enter="handleQuery"
          class="!w-240px"
        />
      </el-form-item>
      <el-form-item label="内容类型" prop="contentType">
        <el-select
          v-model="queryParams.contentType"
          placeholder="请选择内容类型"
          clearable
          class="!w-240px"
        >
          <el-option label="文本" :value="1" />
          <el-option label="图文" :value="2" />
          <el-option label="视频" :value="3" />
          <el-option label="音频" :value="4" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button @click="handleQuery"><Icon icon="ep:search" class="mr-5px" /> 搜索</el-button>
        <el-button @click="resetQuery"><Icon icon="ep:refresh" class="mr-5px" /> 重置</el-button>
      </el-form-item>
    </el-form>
  </ContentWrap>

  <ContentWrap>
    <div class="flex h-[calc(100vh-280px)]">
      <!-- 左侧分组列表 -->
      <div class="w-250px border-r pr-4">
        <div class="mb-4 flex items-center justify-between">
          <span class="text-base font-bold">话术分组</span>
          <el-button type="primary" size="small" @click="openGroupForm('create')" v-hasPermi="['social-matrix:script-group:create']">
            <Icon icon="ep:plus" />
          </el-button>
        </div>
        <el-scrollbar height="calc(100vh - 360px)">
          <div
            v-for="group in groupList"
            :key="group.id"
            class="mb-2 cursor-pointer rounded px-3 py-2 hover:bg-gray-100"
            :class="{ 'bg-blue-50': selectedGroupId === group.id }"
            @click="handleSelectGroup(group.id)"
          >
            <div class="flex items-center justify-between">
              <span class="truncate">{{ group.groupName }}</span>
              <el-dropdown trigger="click" @command="(cmd: string) => handleGroupCommand(cmd, group)">
                <Icon icon="ep:more" class="cursor-pointer" />
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="edit" v-hasPermi="['social-matrix:script-group:update']">编辑</el-dropdown-item>
                    <el-dropdown-item command="delete" v-hasPermi="['social-matrix:script-group:delete']">删除</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </div>
            <div class="mt-1 text-xs text-gray-400">
              <el-tag v-if="group.groupType === 1" size="small" type="primary">公共</el-tag>
              <el-tag v-else size="small" type="warning">私有</el-tag>
            </div>
          </div>
        </el-scrollbar>
      </div>

      <!-- 右侧话术列表 -->
      <div class="flex-1 pl-4">
        <div class="mb-4 flex items-center justify-between">
          <h3 class="text-lg font-bold">{{ selectedGroupName || '全部话术' }}</h3>
          <el-button
            type="primary"
            @click="openScriptForm('create')"
            v-hasPermi="['social-matrix:script:create']"
          >
            <Icon icon="ep:plus" class="mr-5px" /> 添加话术
          </el-button>
        </div>

        <el-table v-loading="loading" :data="list" :stripe="true" :show-overflow-tooltip="true" max-height="calc(100vh - 400px)">
          <el-table-column label="话术标题" align="center" prop="scriptTitle" min-width="150" />
          <el-table-column label="话术内容" align="center" prop="scriptContent" min-width="300" show-overflow-tooltip />
          <el-table-column label="内容类型" align="center" prop="contentType" width="100">
            <template #default="scope">
              <el-tag v-if="scope.row.contentType === 1" type="primary">文本</el-tag>
              <el-tag v-else-if="scope.row.contentType === 2" type="success">图文</el-tag>
              <el-tag v-else-if="scope.row.contentType === 3" type="warning">视频</el-tag>
              <el-tag v-else type="info">音频</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="排序" align="center" prop="sort" width="80" />
          <el-table-column label="创建时间" align="center" prop="createTime" width="180" />
          <el-table-column label="操作" align="center" width="150" fixed="right">
            <template #default="scope">
              <el-button
                link
                type="primary"
                @click="openScriptForm('update', scope.row.id)"
                v-hasPermi="['social-matrix:script:update']"
              >
                编辑
              </el-button>
              <el-button
                link
                type="danger"
                @click="handleDeleteScript(scope.row.id)"
                v-hasPermi="['social-matrix:script:delete']"
              >
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>
        
        <!-- 分页 -->
        <Pagination
          :total="total"
          v-model:page="queryParams.pageNo"
          v-model:limit="queryParams.pageSize"
          @pagination="getScriptList"
        />
      </div>
    </div>
  </ContentWrap>

  <!-- 分组表单弹窗 -->
  <ScriptGroupForm ref="groupFormRef" @success="handleGroupSuccess" />

  <!-- 话术表单弹窗 -->
  <ScriptForm ref="scriptFormRef" @success="getScriptList" />
</template>

<script setup lang="ts">
import { getScriptGroupList, deleteScriptGroup } from '@/api/facebook/scriptgroup'
import { getScriptPage, deleteScript, FbScriptPageReqVO, FbScriptVO } from '@/api/facebook/script'
import { FbScriptGroupVO } from '@/api/facebook/scriptgroup'
import ScriptGroupForm from './ScriptGroupForm.vue'
import ScriptForm from './ScriptForm.vue'

const message = useMessage()
const { t } = useI18n()

const loading = ref(false)
const groupList = ref<FbScriptGroupVO[]>([])
const list = ref<FbScriptVO[]>([])
const total = ref(0)
const selectedGroupId = ref<number>()
const selectedGroupName = ref('')

const queryParams = reactive<FbScriptPageReqVO>({
  pageNo: 1,
  pageSize: 10,
  groupId: undefined,
  scriptTitle: undefined,
  contentType: undefined
})
const queryFormRef = ref()

const groupFormRef = ref()
const scriptFormRef = ref()

/** 查询分组列表 */
const getGroupList = async () => {
  try {
    groupList.value = await getScriptGroupList()
    // 默认选中第一个分组
    if (groupList.value.length > 0 && !selectedGroupId.value) {
      handleSelectGroup(groupList.value[0].id)
    }
  } catch {}
}

/** 查询话术列表 */
const getScriptList = async () => {
  loading.value = true
  try {
    const data = await getScriptPage(queryParams)
    list.value = data.list
    total.value = data.total
  } finally {
    loading.value = false
  }
}

/** 选择分组 */
const handleSelectGroup = (groupId: number) => {
  selectedGroupId.value = groupId
  const group = groupList.value.find(g => g.id === groupId)
  selectedGroupName.value = group?.groupName || ''
  queryParams.groupId = groupId
  queryParams.pageNo = 1
  getScriptList()
}

/** 分组操作 */
const handleGroupCommand = (command: string, group: FbScriptGroupVO) => {
  if (command === 'edit') {
    openGroupForm('update', group.id)
  } else if (command === 'delete') {
    handleDeleteGroup(group.id)
  }
}

/** 删除分组 */
const handleDeleteGroup = async (id: number) => {
  try {
    await message.delConfirm()
    await deleteScriptGroup(id)
    message.success(t('common.delSuccess'))
    if (selectedGroupId.value === id) {
      selectedGroupId.value = undefined
      selectedGroupName.value = ''
      list.value = []
      total.value = 0
    }
    await getGroupList()
  } catch {}
}

/** 分组保存成功 */
const handleGroupSuccess = () => {
  getGroupList()
}

/** 打开分组表单 */
const openGroupForm = (type: string, id?: number) => {
  groupFormRef.value.open(type, id)
}

/** 打开话术表单 */
const openScriptForm = (type: string, id?: number) => {
  scriptFormRef.value.open(type, id, selectedGroupId.value)
}

/** 删除话术 */
const handleDeleteScript = async (id: number) => {
  try {
    await message.delConfirm()
    await deleteScript(id)
    message.success(t('common.delSuccess'))
    await getScriptList()
  } catch {}
}

/** 搜索按钮操作 */
const handleQuery = () => {
  queryParams.pageNo = 1
  getScriptList()
}

/** 重置按钮操作 */
const resetQuery = () => {
  queryFormRef.value.resetFields()
  handleQuery()
}

/** 初始化 */
onMounted(() => {
  getGroupList()
})
</script>
