const path = require("path");
const { expect } = require("@playwright/test");

const VIEWER_IMAGE_FIXTURES = [
  { key: "portrait916", width: 900, height: 1600, path: path.resolve(__dirname, "../../fixtures/viewer-ratios/portrait-9x16.png") },
  { key: "portrait23", width: 1000, height: 1500, path: path.resolve(__dirname, "../../fixtures/viewer-ratios/portrait-2x3.png") },
  { key: "square", width: 1200, height: 1200, path: path.resolve(__dirname, "../../fixtures/viewer-ratios/square-1x1.png") },
  { key: "landscape43", width: 1600, height: 1200, path: path.resolve(__dirname, "../../fixtures/viewer-ratios/landscape-4x3.png") },
  { key: "wide169", width: 1600, height: 900, path: path.resolve(__dirname, "../../fixtures/viewer-ratios/wide-16x9.png") },
  { key: "panorama219", width: 2100, height: 900, path: path.resolve(__dirname, "../../fixtures/viewer-ratios/panorama-21x9.png") },
];

const VIEWER_IMAGE_PATHS = VIEWER_IMAGE_FIXTURES.map((fixture) => fixture.path);

async function getAuthToken(page) {
  const token = await page.evaluate(() => localStorage.getItem("authToken"));
  expect(token, "Expected an auth token in localStorage for viewer setup.").toBeTruthy();
  return token;
}

async function sendAuthedRequest(page, request, method, url, data) {
  const token = await getAuthToken(page);
  const response = await request.fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
    },
    data,
  });

  expect(response.ok(), `${method} ${url} failed with HTTP ${response.status()}`).toBeTruthy();
  return response;
}

function getPathSegment(url, prefix) {
  const pathname = new URL(url).pathname;
  if (!pathname.startsWith(prefix)) {
    throw new Error(`Expected URL path to start with '${prefix}' but received '${pathname}'.`);
  }

  return decodeURIComponent(pathname.slice(prefix.length));
}

async function createRichViewerPost(page, request, suffix, options = {}) {
  const title = options.title || `Viewer showcase ${suffix}`;
  const content =
    options.content ||
    "Built to exercise the post-details flow with a separate rich viewer overlay, integrated commentary, anchored marks, and a full aspect-ratio matrix.";
  const tags = options.tags || "viewer, moonlight, showcase";

  await page.goto("/posts/new");
  await page.getByTestId("create-post-title").fill(title);
  await page.getByTestId("create-post-content").fill(content);
  await page.getByTestId("create-post-tags").fill(tags);
  await page.getByTestId("create-post-images").setInputFiles(options.imagePaths || VIEWER_IMAGE_PATHS);
  await page.getByTestId("create-post-submit").click();

  await expect(page).toHaveURL(/\/posts\/\d+$/);
  await expect(page.getByTestId("post-title")).toHaveText(title);

  const postId = Number.parseInt(getPathSegment(page.url(), "/posts/"), 10);
  expect(Number.isInteger(postId)).toBeTruthy();

  const viewerResponse = await sendAuthedRequest(page, request, "GET", `/api/posts/${postId}/viewer`);
  const viewer = await viewerResponse.json();
  expect(Array.isArray(viewer?.images)).toBeTruthy();
  expect(viewer.images.length, "Expected the rich viewer setup to create the full aspect-ratio matrix.").toBeGreaterThanOrEqual(VIEWER_IMAGE_FIXTURES.length);

  const imagesByKey = Object.fromEntries(
    VIEWER_IMAGE_FIXTURES.map((fixture) => [
      fixture.key,
      viewer.images.find((image) => Math.abs((image.width / image.height) - (fixture.width / fixture.height)) < 0.01),
    ]),
  );

  for (const fixture of VIEWER_IMAGE_FIXTURES) {
    expect(imagesByKey[fixture.key], `Expected fixture '${fixture.key}' to be present in the viewer payload.`).toBeTruthy();
  }

  const primaryImage = imagesByKey.portrait916;
  const secondaryImage = imagesByKey.portrait23;
  const squareImage = imagesByKey.square;
  const panoramaImage = imagesByKey.panorama219;

  const authorMarkResponse = await sendAuthedRequest(
    page,
    request,
    "POST",
    `/api/posts/${postId}/images/${primaryImage.id}/author-marks`,
    {
      normalizedX: 0.34,
      normalizedY: 0.47,
      tag: "blend note",
      message: "Cool blue is introduced before the highlight pass to keep the nocturne skin readable.",
    },
  );
  const authorMark = await authorMarkResponse.json();

  const squareAuthorMarkResponse = await sendAuthedRequest(
    page,
    request,
    "POST",
    `/api/posts/${postId}/images/${squareImage.id}/author-marks`,
    {
      normalizedX: 0.52,
      normalizedY: 0.51,
      tag: "center anchor",
      message: "Square framing should keep author notes centered and stable during zoom and resize.",
    },
  );
  const squareAuthorMark = await squareAuthorMarkResponse.json();

  const panoramaAuthorMarkResponse = await sendAuthedRequest(
    page,
    request,
    "POST",
    `/api/posts/${postId}/images/${panoramaImage.id}/author-marks`,
    {
      normalizedX: 0.78,
      normalizedY: 0.6,
      tag: "pan sweep",
      message: "Panorama notes must remain anchored even when the image is letterboxed inside the stage.",
    },
  );
  const panoramaAuthorMark = await panoramaAuthorMarkResponse.json();

  const markedCommentOneResponse = await sendAuthedRequest(page, request, "POST", `/api/posts/${postId}/comments`, {
    postId,
    text: "Portrait follow-up anchor sits on the second portrait image and should switch the viewer there cleanly.",
    mark: {
      postImageId: secondaryImage.id,
      normalizedX: 0.66,
      normalizedY: 0.38,
    },
  });
  const markedCommentOne = await markedCommentOneResponse.json();

  const markedCommentTwoResponse = await sendAuthedRequest(page, request, "POST", `/api/posts/${postId}/comments`, {
    postId,
    text: "Square image anchor should stay centered while the comment panel highlights this thread.",
    mark: {
      postImageId: squareImage.id,
      normalizedX: 0.48,
      normalizedY: 0.52,
    },
  });
  const markedCommentTwo = await markedCommentTwoResponse.json();

  const markedCommentThreeResponse = await sendAuthedRequest(page, request, "POST", `/api/posts/${postId}/comments`, {
    postId,
    text: "Panorama anchor is here to verify active-comment switching on a very wide image.",
    mark: {
      postImageId: panoramaImage.id,
      normalizedX: 0.74,
      normalizedY: 0.61,
    },
  });
  const markedCommentThree = await markedCommentThreeResponse.json();

  const plainCommentResponse = await sendAuthedRequest(page, request, "POST", `/api/posts/${postId}/comments`, {
    postId,
    text: "The right-panel commentary should stay useful without overpowering the image stage.",
  });
  const plainComment = await plainCommentResponse.json();

  await page.goto(`/posts/${postId}`);
  await page.waitForLoadState("networkidle");

  return {
    postId,
    title,
    viewer,
    imagesByKey,
    primaryImage,
    secondaryImage,
    squareImage,
    panoramaImage,
    authorMark,
    squareAuthorMark,
    panoramaAuthorMark,
    markedCommentOne,
    markedCommentTwo,
    markedCommentThree,
    plainComment,
  };
}

async function openViewerFromDetails(page, options = {}) {
  const { waitForImage = true } = options;
  await page.getByTestId("post-details-open-viewer-hero").click();
  await expect(page.getByTestId("rich-image-viewer-modal")).toBeVisible();
  await expect(page.getByTestId("viewer-control-rail")).toBeVisible();
  await expect(page.getByTestId("viewer-stage")).toBeVisible();
  if (waitForImage) {
    await expect(page.getByTestId("viewer-stage-image")).toBeVisible();
  }
  await expect(page.getByTestId("viewer-side-panel")).toBeVisible();
}

module.exports = {
  VIEWER_IMAGE_PATHS,
  createRichViewerPost,
  getPathSegment,
  openViewerFromDetails,
  sendAuthedRequest,
};
