# FaceAttribNet

Free, locally hosted facial attribute classifier. No API key or third-party inference endpoint is used.

- Model: https://github.com/yakhyo/face-attribute/releases/download/weights/face_attrib_net.onnx
- Source / preprocessing / output order: https://github.com/yakhyo/face-attribute/blob/main/models/onnx_model.py
- Original Qualcomm model: https://github.com/qualcomm/ai-hub-models/tree/main/qai_hub_models/models/face_attrib_net
- License: BSD 3-Clause; see LICENSE in this directory.

Input: face crop letterboxed to 128×128, RGB, NCHW float32 [0,1]. Output: five independent probabilities in this order: left eye open, right eye open, eyeglasses, mask, sunglasses. Normalization is already part of the graph.

App policy blocks accessories at probability >=0.5 and treats >=0.2 as uncertain, which also blocks progress. Both eyes must score >=0.65 for an accepted frame. These are application thresholds, not an accuracy or security guarantee. Tests on public sample images do not establish performance across all people, coverings, camera devices or lighting conditions. Client checks can be bypassed and never replace admin review.
