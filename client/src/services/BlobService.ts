import type { Id } from '@/models';
import HttpServiceV1 from './HttpServiceV1';

export default class BlobService {
  static async getPresignedUrl(id: Id, token: string | null | undefined = null): Promise<string> {
    return await HttpServiceV1.getText(`blob?url=${id}`, token);
  }
};
